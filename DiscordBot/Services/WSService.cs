using Discord;
using DiscordBot.Websockets;
#if INCLUDE_CHESS
using DiscordBot.WebSockets;
#endif
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

#if WINDOWS
        public static string Url = $"ws://{MLAPI.Handler.LocalAPIDomain}:4650";
#else
        public static string Url = $"wss://{MLAPI.Handler.LocalAPIDomain.Replace("mlapi", "wss")}/wss";
#endif

        public override void OnLoaded(IServiceProvider services)
        {
            if (Server != null)
                return;
            Server = new WebSocketServer(System.Net.IPAddress.Any, 4650);
            Server.AllowForwardedRequest = true;
            // Server.AddWebSocketService<Chat>("/Chat");
#if INCLUDE_CHESS
            Server.AddWebSocketService<ChessConnection>("/chess");
            Server.AddWebSocketService<ChessNotifyWS>("/chess-monitor", x =>
            {
                x.Service = Program.Services.GetRequiredService<ChessService>();
            });
            Server.AddWebSocketService<ChessTimeWS>("/chess-timer");
            Server.AddWebSocketService<MLServer>("/masterlist", x =>
            {
                x.Service = Program.Services.GetRequiredService<MLService>();
            });
#endif
            Server.AddWebSocketService<GroupGameWS>("/group-game");
            Server.AddWebSocketService<LogWS>("/log");
            Server.AddWebSocketService<BanAppealsWS>("/ban-appeal");
            Server.AddWebSocketService<TimeTrackerWS>("/time-tracker");
            Server.AddWebSocketService<StatisticsWS>("/statistics");
            Server.AddWebSocketService<MasterlistWS>("/masterlist");
            Server.AddWebSocketService<FoodWS>("/food");
            Server.AddWebSocketService<FoodScanWS>("/food-scan");
            //Server.Log.Level = WebSocketSharp.LogLevel.Trace;
            Server.Log.Output = (x, y) =>
            {
                var msg = new LogMessage((LogSeverity)x.Level, x.Caller.ToString(), $"{x.Message}\r\n=> {y}");
                Program.LogMsg(msg);
            };
            Server.Start();
            if (Server.IsListening)
            {
                Program.LogInfo($"Listening on port {Server.Port} and proving WebSocket services", "WSS");
                foreach (var path in Server.WebSocketServices.Paths)
                {
                    Program.LogInfo("- " + path, "WSS");
                }
            }
        }

        public override void OnClose()
        {
            try
            {
                Server?.Stop(WebSocketSharp.CloseStatusCode.Away, "Exiting");
            } catch { }
        }

    }
}