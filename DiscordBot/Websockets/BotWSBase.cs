using DiscordBot.Classes;
using DiscordBot.MLAPI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace DiscordBot.Websockets
{
    public abstract class BotWSBase : WebSocketBehavior
    {
        public string SessionToken { get
            {
                string strToken = null;
                var cookie = Context.CookieCollection[AuthSession.CookieName];
                strToken ??= cookie?.Value;
                strToken ??= Context.QueryString.Get(AuthSession.CookieName);
                strToken ??= Context.Headers.Get($"X-{AuthSession.CookieName.ToUpper()}");
                return strToken;
            } }

        public string ApiToken { get
            {
                string strToken = null;
                var cookie = Context.CookieCollection["api-key"];
                strToken ??= cookie?.Value;
                strToken ??= Context.QueryString.Get("api-key");
                strToken ??= Context.Headers.Get($"X-{"api-key"}");
                return strToken;
            } }

        string _ip = null;
        public string IP
        {
            get
            {
                _ip ??= Context.Headers["X-Forwarded-For"] ?? Context.UserEndPoint.Address.ToString();
                return _ip;
            }
        }

        private BotUser user;
        public BotUser User { get
            {
                if (user != null)
                    return user;
                if (!Handler.findSession(SessionToken, out var usr, out _))
                {
                    if(!Handler.findToken(ApiToken, out usr, out _))
                    {
                        Program.LogDebug($"{IP} provided an unknown session or auth token, or none at all.", "WSLog");
                        return null;
                    }
                }
                user = usr;
                return user;
            } }

        public string Type => this.GetType().Name;

        protected virtual void Log(string message, Discord.LogSeverity severity, string source = null)
        {
            var lgm = new Discord.LogMessage(severity,
                Type + (source != null ? "/" + source : ""),
                message);
            Program.LogMsg(lgm);
        }

        protected virtual void Debug(string message, string source = null)
            => Log(message, Discord.LogSeverity.Debug, source);
        protected virtual void Info(string message, string source = null)
            => Log(message, Discord.LogSeverity.Info, source);
        protected virtual void Warn(string message, string source = null)
            => Log(message, Discord.LogSeverity.Warning, source);
    }

    public abstract class BotPacketWSBase<TPacketId> : BotWSBase
        where TPacketId: Enum
    {

        protected override void OnMessage(MessageEventArgs e)
        {
            var packet = new Packet<TPacketId>(JObject.Parse(e.Data));
            OnPacket(packet);
        }

        public void Send(Packet<TPacketId> packet)
        {
            Send(packet.ToString());
        }

        protected abstract void OnPacket(Packet<TPacketId> packet);
    }
}
