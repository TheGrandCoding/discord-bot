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

        public BotUser User { get; set; }
        public TimeTrackDb DB { get; private set; }
        public Cached<bool> WatchingVideo { get; private set; }

        public int GetInterval = 0;
        public int SetInterval = 0;

        public void SendAllUserRatelimits()
        {
            var sameClient = Sessions.Sessions.Cast<TimeTrackerWS>().Where(x => x.User?.Id == User.Id).ToList();
            var numWatching = sameClient.Count(x => x.WatchingVideo.GetValueOrDefault(false));
            if (numWatching <= 0)
                numWatching = 1;
            var toGet = (DefaultGetInterval * numWatching) + (1000 * Sessions.Count - 1);
            var toSet = (DefaultSetInterval * numWatching) + (1000 * Sessions.Count - 1);
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
            DB = Program.Services.GetRequiredService<TimeTrackDb>();
            WatchingVideo = new Cached<bool>(false, 2);
            SendAllUserRatelimits();
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            Program.LogMsg(e.Data, LogSeverity.Debug, $"TTWS-{IP}");
            var packet = new TTPacket(JObject.Parse(e.Data));

            JObject jobj;
            if(packet.Id == TTPacketId.GetTimes)
            {
                jobj = new JObject();
                var content = packet.Content as JArray;
                foreach(JToken token in content)
                {
                    var id = token.ToObject<string>();
                    var thing = DB.Get(User.Id, id);
                    jobj[id] = thing?.WatchedTime ?? 0d;
                }
                Send(packet.ReplyWith(jobj));
            } else if(packet.Id == TTPacketId.SetTimes)
            {
                jobj = packet.Content as JObject;
                foreach (JProperty token in jobj.Children())
                {
                    var val = token.Value.ToObject<double>();
                    DB.Add(User.Id, token.Name, val);
                }
                DB.SaveChanges();
                Send(packet.ReplyWith(JValue.FromObject("OK")));
                WatchingVideo.Value = true;
                SendAllUserRatelimits();
            } else if(packet.Id == TTPacketId.GetVersion)
            {
                string version = TimeTrackDb.ExtensionVersion.GetValueOrDefault(null);
                if(version == null)
                {
                    var client = Program.Services.GetRequiredService<HttpClient>();
                    var r = client.GetAsync("https://api.github.com/repos/CheAle14/time-tracker/releases/latest").Result;
                    if (r.IsSuccessStatusCode)
                    {
                        jobj = Newtonsoft.Json.Linq.JObject.Parse(r.Content.ReadAsStringAsync().Result);
                        var s = jobj["tag_name"].ToObject<string>();
                        if (s.StartsWith("v"))
                            s = s[1..];
                        TimeTrackDb.ExtensionVersion.Value = s;
                    }
                    else
                    {
                        TimeTrackDb.ExtensionVersion.Value = "0.0";
                    }
                    version = TimeTrackDb.ExtensionVersion.Value;
                }
                Send(packet.ReplyWith(JValue.FromObject(version)));
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
        DirectRatelimit
    }
}
