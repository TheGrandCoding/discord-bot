﻿using Discord;
using DiscordBot.Classes;
using DiscordBot.MLAPI;
using DiscordBot.MLAPI.Modules.TimeTracking;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace DiscordBot.Websockets
{
    public class TimeTrackerWS : WebSocketBehavior
    {
        public const int DefaultGetInterval = 15_000;
        public const int DefaultSetInterval = 30_000;

        public static void BroadcastUpdate(string version, WebSocketSessionManager host)
        {
            host.Broadcast(new TTPacket(TTPacketId.SendVersion, JToken.FromObject(version))
                .ToString()
                );
        }

        public BotUser User { get; set; }
        public Cached<bool> WatchingVideo { get; private set; }
        public ConcurrentDictionary<uint, TTPacket> IndempotencyCache { get; private set; } = new ConcurrentDictionary<uint, TTPacket>();
        public int APIVersion { get; private set; }

        public int GetInterval = 0;
        public int SetInterval = 0;

        public List<TimeTrackerWS> GetSameUsers()
        {
            return Sessions.Sessions.Cast<TimeTrackerWS>().Where(x => x.User?.Id == User.Id).ToList();
        }

        public void SendAllClientRatelimits()
        {
            var sameClient = GetSameUsers();
            var numWatching = sameClient.Count(x => x.WatchingVideo.GetValueOrDefault(false));
            if (numWatching <= 0)
                numWatching = 1;
            var toGet = (DefaultGetInterval * numWatching) + (1000 * (Sessions.Count - 1));
            var toSet = (DefaultSetInterval * numWatching) + (1000 * (Sessions.Count - 1));
            foreach (var client in sameClient)
                client.SendRatelimits(toSet, toGet);
        }

        public void SendRatelimits(int toSet, int toGet)
        {
            var jobj = new JObject();
            if(GetInterval != toGet)
            {
                jobj["get"] = toGet;
                GetInterval = toGet;
            } 
            if(SetInterval != toSet)
            {
                jobj["set"] = toSet;
                SetInterval = toSet;
            }
            if(jobj.Count > 0)
            {
                Send(new TTPacket(TTPacketId.DirectRatelimit, jobj));
            }
        }

        public void SendIgnoredDatas(TimeTrackDb DB)
        {
            var ignored = DB.GetIgnoreDatas(User.Id);
            if (ignored.Length == 0)
                return;
            var jobj = new JObject();
            foreach(var ignore in ignored)
            {
                jobj[ignore.VideoId] = true;
            }
            var packet = new TTPacket(TTPacketId.UpdateIgnored, jobj);
            Send(packet);
        }

        string _ip = null;
        public string IP
        {
            get
            {
                _ip ??= Context.Headers["X-Forwarded-For"] ?? Context.UserEndPoint.Address.ToString();
                return _ip;
            }
        }

        protected void Send(Packet<TTPacketId> obj)
        {
            Send(obj.ToString());
        }

        protected override void OnOpen()
        {
            string strToken = null;
            var cookie = Context.CookieCollection[AuthToken.SessionToken];
            strToken ??= cookie?.Value;
            strToken ??= Context.QueryString.Get(AuthToken.SessionToken);
            strToken ??= Context.Headers.Get($"X-{AuthToken.SessionToken.ToUpper()}");
            if (!Handler.findToken(strToken, out var usr, out _))
            {
                Program.LogDebug($"{IP} attempted unauthorized time tracking", "WSTT");
                Context.WebSocket.Close(CloseStatusCode.Normal, "Unauthorized");
                return;
            }
            User = usr;
            if (!int.TryParse(Context.QueryString.Get("v"), out var x))
                x = 1;
            APIVersion = x;
            using var db = Program.Services.GetRequiredService<TimeTrackDb>();
            WatchingVideo = new Cached<bool>(false, 2);
            SendAllClientRatelimits();
            SendIgnoredDatas(db);
        }

        TTPacket getResponse(TTPacket packet, TimeTrackDb DB)
        {
            JObject jobj;
            if (packet.Id == TTPacketId.GetTimes)
            {
                jobj = new JObject();
                var content = packet.Content as JArray;
                foreach (JToken token in content)
                {
                    var id = token.ToObject<string>();
                    var thing = DB.GetVideo(User.Id, id);
                    jobj[id] = thing?.WatchedTime ?? 0d;
                }
                return packet.ReplyWith(jobj);
            }
            else if (packet.Id == TTPacketId.SetTimes)
            {
                jobj = packet.Content as JObject;
                foreach (JProperty token in jobj.Children())
                {
                    var val = token.Value.ToObject<double>();
                    DB.AddVideo(User.Id, token.Name, val);
                }
                DB.SaveChanges();
                WatchingVideo.Value = true;
                SendAllClientRatelimits();
                return packet.ReplyWith(JValue.CreateNull());
            }
            else if (packet.Id == TTPacketId.GetVersion)
            {
                string version = TimeTrackDb.GetExtensionVersion();
                return packet.ReplyWith(JValue.FromObject(version));
            }
            else if (packet.Id == TTPacketId.GetLatest)
            {
                jobj = new JObject();
                var videos = DB.WatchTimes.AsQueryable().OrderByDescending(x => x.LastUpdated).Take(5).ToList();
                var ids = videos.Select(x => x.VideoId).ToArray();
                var videoInfo = TimeTrackDb.GetVideoInformation(ids);
                foreach (var vid in videos)
                {
                    var vidObj = new JObject();
                    vidObj["saved"] = (int)vid.WatchedTime;
                    vidObj["when"] = new DateTimeOffset(vid.LastUpdated).ToUnixTimeMilliseconds();
                    if (videoInfo.TryGetValue(vid.VideoId, out var info))
                    {
                        vidObj["title"] = info.Snippet.Title;
                        vidObj["author"] = info.Snippet.ChannelTitle;
                    }
                    jobj[vid.VideoId] = vidObj;
                }
                return packet.ReplyWith(jobj);
            }
            else if (packet.Id == TTPacketId.VisitedThread)
            {
                var threadId = packet.Content["id"].ToObject<string>();
                var comments = packet.Content["count"].ToObject<int>();
                DB.AddThread(User.Id, threadId, comments);
                return packet.ReplyWith(JValue.CreateNull());
            }
            else if (packet.Id == TTPacketId.GetThreads)
            {
                jobj = new JObject();
                var content = packet.Content as JArray;
                foreach (JToken token in content)
                {
                    var id = token.ToObject<string>();
                    var threads = DB.GetThread(User.Id, id);
                    if (threads == null || threads.Length == 0)
                        continue;
                    var thing = threads.Last();
                    var threadObj = new JObject();
                    if(APIVersion == 2)
                    {
                        var jar = new JArray();
                        foreach (var x in threads)
                            jar.Add(new DateTimeOffset(x.LastUpdated).ToUnixTimeMilliseconds());
                        threadObj["when"] = jar;
                    } else
                    {
                        threadObj["when"] = new DateTimeOffset(thing.LastUpdated).ToUnixTimeMilliseconds();
                    }
                    threadObj["count"] = thing.Comments;
                    jobj[id] = threadObj;
                }
                return packet.ReplyWith(jobj);
            }
            else if (packet.Id == TTPacketId.UpdateIgnored)
            {
                foreach (var item in packet.Content as JObject)
                {
                    var id = item.Key;
                    var isIgnored = item.Value.ToObject<bool>();
                    DB.AddIgnored(User.Id, id, isIgnored);
                }
                foreach (var wsConn in GetSameUsers())
                {
                    if (wsConn.ID == this.ID)
                        continue;
                    wsConn.Send(packet);
                }
                return packet.ReplyWith(JValue.CreateNull());
            } else
            {
                return packet.ReplyWith(JValue.CreateString("Unknown packet type"));
            }
        }

        Cached<bool> sentError = new Cached<bool>(false);
        protected override void OnMessage(MessageEventArgs e)
        {
            Program.LogDebug(e.Data, $"TTWS-{IP}");
            var packet = new TTPacket(JObject.Parse(e.Data));

            using var db = Program.Services.GetRequiredService<TimeTrackDb>();
            try
            {
                TTPacket response;
                if(IndempotencyCache.TryGetValue(packet.Sequence, out response))
                {
                    Program.LogWarning($"Answering packet {packet.Sequence} with previously sent packet ({response.Response}, {response.ResentCount}), perhaps we disconnected?", "TTWS-" + IP);
                    response.ResentCount += 1;
                } else
                {
                    response = getResponse(packet, db);
                    if(IndempotencyCache.Count > 4)
                    { // clear oldest cached item
                        var smallest = IndempotencyCache.Keys.OrderByDescending(x => x).First();
                        IndempotencyCache.TryRemove(smallest, out _);
                    }
                    IndempotencyCache[packet.Sequence] = response;
                }
                var str = response.ToString();
#if DEBUG
                Program.LogDebug($"Response: " + str, $"TTWS-{IP}");
#endif
                Send(str);
            } catch(Exception ex)
            {
                Program.LogError(ex, $"TimeTrack-{User.Name}");
                if (sentError.GetValueOrDefault(false))
                    return;
                sentError.Value = true;
                try
                {
                    var embed = new EmbedBuilder();
                    embed.Title = "WS Error Occured";
                    embed.Description = ex.Message;
                    embed.Footer = new EmbedFooterBuilder().WithText($"{packet.Id}");
                    User.FirstValidUser.SendMessageAsync(embed: embed.Build()).Wait();
                } catch { }
            } finally
            {
                db.SaveChanges();
            }
        }
    }

    public class TTPacket : Packet<TTPacketId>
    {
        public TTPacket(JObject jObj) : base(jObj)
        {
        }

        public TTPacket(TTPacketId id, JToken token) : base(id, token)
        {
        }
    
        public new TTPacket ReplyWith(JToken content)
        {
            var pong = new TTPacket(Id, content);
            pong.Sequence = getNext();
            pong.Response = Sequence;
            return pong;
        }

        public int ResentCount { get; set; } = 0;

        public override JObject ToJson()
        {
            var json = base.ToJson();
            if(ResentCount > 0)
            {
                json["retry"] = ResentCount;
            }
            return json;
        }
    }

    public enum TTPacketId
    {
        GetTimes,
        SetTimes,
        GetVersion,
        SendVersion,
        DirectRatelimit,
        GetLatest,
        SendLatest,
        VisitedThread,
        GetThreads,
        UpdateIgnored,
    }
}
