using Discord;
using DiscordBot.Classes;
using DiscordBot.MLAPI;
using DiscordBot.MLAPI.Modules.TimeTracking;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace DiscordBot.Websockets
{
    public class TimeTrackerWS : WebSocketBehavior
    {
        public BotUser User { get; set; }
        public TimeTrackDb DB { get; private set; }

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
    }
}
