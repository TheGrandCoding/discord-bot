using Discord;
using DiscordBot.Websockets;
using DiscordBot.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using WebSocketSharp.Server;

namespace DiscordBot.Services
{
    public class WSService : Service
    {
        public static WebSocketServer Server { get; set; }

        public override void OnLoaded()
        {
            if (Server != null)
                return;
            Server = new WebSocketServer(System.Net.IPAddress.Any, 4650);
            // Server.AddWebSocketService<Chat>("/Chat"); // add a '/Feedback' for the Pi-Hole at Marj's?
            Server.AddWebSocketService<ChessConnection>("/chess");
            Server.AddWebSocketService<MLServer>("/masterlist", x =>
            {
                x.Service = Program.Services.GetRequiredService<MLService>();
            });
            Server.AddWebSocketService<ChessNotifyWS>("/chess-monitor", x =>
            {
                x.Service = Program.Services.GetRequiredService<ChessService>();
            });
            Server.AddWebSocketService<ChessTimeWS>("/chess-timer");
            Server.AddWebSocketService<GroupGameWS>("/group-game");
            //Server.Log.Level = LogLevel.Trace;
            Server.Log.Output = (x, y) =>
            {
                Program.LogMsg($"{x.Message}\r\n=> {y}", (LogSeverity)x.Level, "WS-" + x.Caller.ToString());
            };
            Server.Start();
            if (Server.IsListening)
            {
                Program.LogMsg($"Listening on port {Server.Port} and proving WebSocket services");
                foreach (var path in Server.WebSocketServices.Paths)
                {
                    Program.LogMsg("- " + path);
                }
            }
        }

        public override void OnClose()
        {
            try
            {
                Server.RemoveWebSocketService("/chess");
                Server.RemoveWebSocketService("/masterlist");
                Server.Stop();
            } catch { }
        }

    }
}
