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
    public class LogWS : BotWSBase
    {
        public void Handle(object sender, LogMessage msg)
        {
            Send(msg);
        }
        void Send(Program.LogWithTime msg)
        {
            var json = new JObject();
            json["message"] = Program.FormatLogMessage(msg);
            json["colour"] = Program.getColor(msg.Severity).ToString().ToLower();
            try
            {
                this.Send(json.ToString());
            } catch (Exception ex) { }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Program.Log -= Handle;
            if(User != null)
                Program.LogInfo($"{User.Name} is no longer watching the logs via WS", IP);
            base.OnClose(e);
        }
        protected override void OnOpen()
        {
            if(User == null)
            {
                Program.LogDebug($"{IP} attempted unknown WS log.", "WSLog");
                Context.WebSocket.Close(CloseStatusCode.Normal, "Unauthorized");
                return;
            }
            if(!PermChecker.UserHasPerm(User, Perms.Bot.Developer.SeeLatestLog))
            {
                Program.LogWarning($"{IP}, {User.Name} ({User.Id}) attempted forbidden log connection", "WsLog");
                Context.WebSocket.Close(CloseStatusCode.Normal, "Forbidden");
                return;
            }
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
