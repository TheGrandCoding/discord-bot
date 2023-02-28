using DiscordBot.Classes;
using DiscordBot.Services.Masterlist;
using System;
using System.Collections.Generic;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json.Linq;
using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Websockets
{
    public class MasterlistWS : BotPacketWSBase<MLPacketId>
    {
        public Server Server { get; set; }
        public MasterlistService Service { get; set; }

        public string Game { get; set; }
        public string Mode { get; set; }
        public bool IsClient { get; set; }

        public bool CaresAbout(string gameName, string gameMode)
        {
            if (IsClient == false)
                return false;

            return (Game == "*" || gameName == Game)
                && (Mode == "*" || gameMode == Mode);
        }
        public bool CaresAbout(Server server)
            => CaresAbout(server.GameName, server.GameMode);

        protected override void OnClose(CloseEventArgs e)
        {
            Warn($"[{ID}] Connection closed {e.Code} {e.WasClean} {e.Reason}");

            if(Server != null)
            {
                Warn($"Server {Server.Name} ({Server.GameName} {Server.ConnectIp}) has lost its WS connection");
                Server.WS = null;
                Service.BroadcastUpdate(Server.GameName, Server.GameMode);
                Server = null;
            }
        }


        protected override void OnOpen()
        {
            Service = Services.GetRequiredService<MasterlistService>();

            Game = Context.QueryString.Get("game");
            if(string.IsNullOrWhiteSpace(Game))
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "Game query missing");
                return;
            }
            var c = Context.QueryString.Get("client");
            if (bool.TryParse(c, out var x))
                IsClient = x;
            Mode = Context.QueryString.Get("mode");
            if (string.IsNullOrWhiteSpace(Mode) && IsClient == true)
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "Mode query missing as client");
                return;
            }

            if(IsClient)
            {
                var servers = Service.GetServers(Game, Mode);
                Send(new Packet<MLPacketId>(MLPacketId.SendServers, servers));
            }
        }

        protected override void OnPacket(Packet<MLPacketId> packet)
        {

            if(packet.Id == MLPacketId.UpsertServer)
            {
                Server server;
                var id = packet.Content["id"]?.ToObject<string>();
                var creating = id == null;
                if(id == null)
                {
                    server = new Server();
                } else
                {
                    server = new Server(id);
                }
                server.WS = this;
                server.Name = packet.Content["name"]?.ToObject<string>() ?? server.Name;

                server.GameName = packet.Content["game"]?.ToObject<string>() 
                    ?? server.GameName ?? Game;

                server.GameMode = packet.Content["mode"]?.ToObject<string>() 
                    ?? server.GameMode ?? Mode;

                server.IpAddress = Context.UserEndPoint.Address;
                if(packet.Content["ip"] != null)
                {
                    IPAddress.TryParse(packet.Content["ip"].ToString(), out var connIp);
                    server.ConnectIp = connIp;
                }
                server.Port = packet.Content["port"]?.ToObject<int>() ?? server.Port;

                var errors = new List<string>();
                if(string.IsNullOrWhiteSpace(server.Name))
                {
                    errors.Add("Missing server name");
                } else if(server.Name.Length > 32)
                {
                    errors.Add("Server name too long");
                }

                if(string.IsNullOrWhiteSpace(server.GameName) || server.GameName.Length > 32)
                {
                    errors.Add("Game name absent or too long");
                }

                if(string.IsNullOrWhiteSpace(server.GameMode) || server.GameMode.Length > 32)
                {
                    errors.Add("Game mode absent or too long");
                }

                if(server.ConnectIp == null)
                {
                    errors.Add("No IP address provided");
                }

                var jobj = new JObject();
                if(errors.Count > 0)
                {
                    jobj["errors"] = JArray.FromObject(errors);
                } else
                {
                    jobj["id"] = server.Id;
                    Service.AddServer(server);
                    Server = server;
                }

                Send(packet.ReplyWith(jobj));
            } else if(packet.Id == MLPacketId.GetServers)
            {
                var game = packet.Content["game"]?.ToObject<string>() ?? Game;
                var mode = packet.Content["mode"]?.ToObject<string>() ?? Mode;

                var content = Service.GetServers(game, mode);

                Send(new Packet<MLPacketId>(MLPacketId.SendServers, content));
            }
        }

    }

    public enum MLPacketId
    {
        /// <summary>
        /// Game server to WS; updates, creates a new, server on the ML
        /// </summary>
        UpsertServer,
        /// <summary>
        /// Game server to WS; updates an existing server
        /// </summary>
        UpdateServer,

        /// <summary>
        /// Game client to WS; fetches servers of a given game, and optionally game mode
        /// </summary>
        GetServers,

        /// <summary>
        /// WS to game client; informs client of server changes to their game
        /// </summary>
        SendServers,
        
    }
}
