using Discord;
using DiscordBot.Classes;
using DiscordBot.MLAPI;
using DiscordBot.Permissions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace DiscordBot.Websockets
{
    public class LogWS : WebSocketBehavior
    {
        public BotUser User { get; set; }
        string _ip = null;
        public string IP
        {
            get
            {
                _ip ??= Context.Headers["X-Forwarded-For"] ?? Context.UserEndPoint.Address.ToString();
                return _ip;
            }
        }
        public void Handle(object sender, LogMessage msg)
        {
            Send(msg);
        }
        void Send(Program.LogWithTime msg)
        {
            var json = new JObject();
            json["message"] = Program.FormatLogMessage(msg);
            json["colour"] = Program.getColor(msg.Severity).ToString().ToLower();
            this.Send(json.ToString());
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Program.Log -= Handle;
            Program.LogInfo($"{User.Name} is no longer watching the logs via WS", IP);
        }
        protected override void OnOpen()
        {
            string strToken = null;
            var cookie = Context.CookieCollection[AuthToken.SessionToken];
            strToken ??= cookie?.Value;
            strToken ??= Context.QueryString.Get(AuthToken.SessionToken);
            strToken ??= Context.Headers.Get($"X-{AuthToken.SessionToken.ToUpper()}");
            if(!Handler.findToken(strToken, out var usr, out _))
            {
                Program.LogDebug($"{IP} attempted unknown WS log.", "WSLog");
                Context.WebSocket.Close(CloseStatusCode.Normal, "Unauthorized");
                return;
            }
            if(!PermChecker.UserHasPerm(usr, Perms.Bot.Developer.SeeLatestLog))
            {
                Program.LogWarning($"{IP}, {usr.Name} ({usr.Id}) attempted forbidden log connection", "WsLog");
                Context.WebSocket.Close(CloseStatusCode.Normal, "Forbidden");
                return;
            }
            User = usr;
            Program.LogInfo($"{User.Name} has begun watching the logs via WS", IP);
            lock (Program._lockObj)
            {
                foreach(var log in Program.lastLogs)
                {
                    Send(log);
                }
            }
            Program.Log += Handle;
        }
    }
}
