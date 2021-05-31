using Discord;
using DiscordBot.Classes;
using DiscordBot.MLAPI;
using DiscordBot.MLAPI.Modules.TimeTracking;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
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
                Program.LogMsg($"{IP} attempted unauthorized time tracking", LogSeverity.Debug, "WSTT");
                Context.WebSocket.Close(CloseStatusCode.Normal, "Unauthorized");
                return;
            }
            User = usr;
            using var db = Program.Services.GetRequiredService<TimeTrackDb>();
            WatchingVideo = new Cached<bool>(false, 2);
            SendAllClientRatelimits();
            SendIgnoredDatas(db);
        }

        void handlePacket(TTPacket packet, TimeTrackDb DB)
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
                Send(packet.ReplyWith(jobj));
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
                Send(packet.ReplyWith(JValue.FromObject("OK")));
                WatchingVideo.Value = true;
                SendAllClientRatelimits();
            }
            else if (packet.Id == TTPacketId.GetVersion)
            {
                string version = TimeTrackDb.GetExtensionVersion();
                Send(packet.ReplyWith(JValue.FromObject(version)));
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
                Send(packet.ReplyWith(jobj));
            }
            else if (packet.Id == TTPacketId.VisitedThread)
            {
                var threadId = packet.Content["id"].ToObject<string>();
                var comments = packet.Content["count"].ToObject<int>();
                DB.AddThread(User.Id, threadId, comments);
                Send(packet.ReplyWith(JValue.FromObject("OK")));
            }
            else if (packet.Id == TTPacketId.GetThreads)
            {
                jobj = new JObject();
                var content = packet.Content as JArray;
                foreach (JToken token in content)
                {
                    var id = token.ToObject<string>();
                    var thing = DB.GetThread(User.Id, id);
                    if (thing == null)
                        continue;
                    var threadObj = new JObject();
                    threadObj["when"] = new DateTimeOffset(thing.LastUpdated).ToUnixTimeMilliseconds();
                    threadObj["count"] = thing.Comments;
                    jobj[id] = threadObj;
                }
                Send(packet.ReplyWith(jobj));
            }
            else if (packet.Id == TTPacketId.UpdateIgnored)
            {
                foreach (var item in packet.Content as JObject)
                {
                    var id = item.Key;
                    var isIgnored = item.Value.ToObject<bool>();
                    DB.AddIgnored(User.Id, id, isIgnored);
                }
                Send(packet.ReplyWith(JToken.FromObject("OK")));
                foreach (var wsConn in GetSameUsers())
                {
                    if (wsConn.ID == this.ID)
                        continue;
                    wsConn.Send(packet);
                }
            }
        }

        Cached<bool> sentError = new Cached<bool>(false);
        protected override void OnMessage(MessageEventArgs e)
        {
            Program.LogMsg(e.Data, LogSeverity.Debug, $"TTWS-{IP}");
            var packet = new TTPacket(JObject.Parse(e.Data));

            using var db = Program.Services.GetRequiredService<TimeTrackDb>();
            try
            {
                handlePacket(packet, db);
            } catch(Exception ex)
            {
                Program.LogMsg(ex, $"TimeTrack-{User.Name}");
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
