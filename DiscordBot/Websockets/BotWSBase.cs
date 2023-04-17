using DiscordBot.Classes;
using DiscordBot.MLAPI;
using DiscordBot.Services;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
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
        public BotWSBase()
        {
            _scope = Program.GlobalServices.CreateScope();
        }
        public string SessionToken { get
            {
                string strToken = null;
                var cookie = Context.CookieCollection[BotDbAuthSession.CookieName];
                strToken ??= cookie?.Value;
                strToken ??= Context.QueryString.Get(BotDbAuthSession.CookieName);
                strToken ??= Context.Headers.Get($"X-{BotDbAuthSession.CookieName.ToUpper()}");
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
                _ip ??= Program.GetIP(Context.Headers, Context.UserEndPoint.Address);
                return _ip;
            }
        }

        private IServiceScope _scope;
        public IServiceProvider Services => _scope?.ServiceProvider ?? null;
        private BotDbContext _db;
        public BotDbContext BotDB { get => _db ??= Services.GetBotDb("BotWSBase"); }

        private BotDbUser user;
        public BotDbUser User { get
            {
                if (user != null)
                    return user;
                var session = BotDB.GetSessionAsync(SessionToken).Result;
                if (session != null)
                    return user = session.User;
                var token = BotDB.GetTokenAsync(ApiToken).Result;
                if (token != null)
                    return user = token.User;
                Program.LogDebug($"{IP} provided an unknown session or auth token, or none at all.", "WSLog");
                return null;
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

#if DEBUG
        public void SendJson(JToken json, bool indent = true)
#else
        public void SendJson(JToken json, bool indent = false)
#endif
        {
            var str = json.ToString(indent ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None);
#if DEBUG
            Debug(str, "WS >>");
#endif
            base.Send(str);
        }

        public void SendToAllOthers(JToken json)
        {
            if(!WSService.Server.WebSocketServices.TryGetServiceHost("/food", out var host))
            {
                Warn($"Unable to get host to broadcast");
                return;
            }
            foreach(var session in host.Sessions.Sessions)
            {
                if(session.ID != this.ID)
                {
                    if (session is BotWSBase botws) botws.SendJson(json);
                    else Warn($"Unable to send to {session.GetType().FullName}", "SendToAllOthers");
                }
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            _scope?.Dispose();
        }
    }

    public abstract class BotPacketWSBase<TPacketId> : BotWSBase
        where TPacketId: Enum
    {

        protected override void OnMessage(MessageEventArgs e)
        {
            var packet = new Packet<TPacketId>(JObject.Parse(e.Data));
            Debug(packet.ToString(Newtonsoft.Json.Formatting.Indented), "<<");
            OnPacket(packet);
        }

        public void Send(Packet<TPacketId> packet)
        {
            Send(packet.ToString());
        }

        protected abstract void OnPacket(Packet<TPacketId> packet);
    }
}
