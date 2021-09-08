using DiscordBot.Classes;
using DiscordBot.Websockets;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace DiscordBot.Services.Masterlist
{
    public class MasterlistService : Service
    {
        public ConcurrentDictionary<string, Server> Servers { get; set; }
            = new ConcurrentDictionary<string, Server>();

        public WSService WSService { get; set; }

        public JArray GetServers(string gameName, string gameMode)
        {
            var content = new JArray();
            foreach (var srv in Servers.Values)
            {
                if (gameName == "*" || srv.GameName == gameName)
                {
                    if(gameMode == null || gameMode == "*" || srv.GameMode == gameMode)
                        content.Add(srv.ToJson());
                }
            }
            return content;
        }

        SemaphoreSlim sema = new SemaphoreSlim(1, 1);

        public void AddServer(Server server)
        {
            sema.Wait();
            try
            {
                var servers = Servers.Values.ToList();
                string ipb = (server.ConnectIp ?? server.IpAddress).ToString();
                var toRemove = new List<string>();
                Func<Server, bool> predicate = x =>
                {
                    if (x.GameName != server.GameName)
                        return false;
                    if (x.GameMode != server.GameMode)
                        return false;
                    string ipx = (x.ConnectIp ?? x.IpAddress).ToString();

                    return ipx == ipb && x.Port == server.Port;
                };
                foreach(var x in Servers.Values)
                {
                    if(predicate(x))
                    {
                        toRemove.Add(x.Id);
                    }
                }
                foreach (var id in toRemove)
                    Servers.TryRemove(id, out _);

                Servers[server.Id] = server;
            } finally
            {
                sema.Release();
            }


            Servers[server.Id] = server;

            BroadcastUpdate(server.GameName, server.GameMode);
        }

        public void BroadcastUpdate(string gameName, string gameMode)
        {
            var content = GetServers(gameName, gameMode);
            var packet = new Packet<MLPacketId>(MLPacketId.SendServers, content);
            WSService.Server.WebSocketServices.TryGetServiceHost("/masterlist", out var host);
            foreach (var client in host.Sessions.Sessions
                .Cast<MasterlistWS>()
                .Where(x => x.CaresAbout(gameName, gameMode))
                )
            {
                client.Send(packet);
            }
        }
    }

    public class Server
    {
        public Server(string id)
        {
            Id = id;
        }
        public Server()
            : this(Guid.NewGuid().ToString())
        {
        }
        public string Id { get; set; }

        public string Name { get; set; }

        public string GameName { get; set; }
        public string GameMode { get; set; }

        /// <summary>
        /// The IP that the active WS connection is through
        /// </summary>
        public IPAddress IpAddress { get; set; }

        /// <summary>
        /// The IP that the server wants clients to connect to
        /// </summary>
        public IPAddress ConnectIp { get; set; }

        public int Port { get; set; }

        public MasterlistWS WS { get; set; }

        public bool Online {  get
            {
                return (WS?.State ?? WebSocketSharp.WebSocketState.Closed)
                    == WebSocketSharp.WebSocketState.Open;
            } }


        public JToken ToJson()
        {
            var obj = new JObject();
            obj["id"] = Id;
            obj["name"] = Name;
            obj["game"] = GameName;
            obj["mode"] = GameMode;
            obj["ip"] = (ConnectIp ?? IpAddress).ToString();
            obj["port"] = Port;
            obj["online"] = Online;
            return obj;
        }


        public override string ToString()
            => ToJson().ToString(Newtonsoft.Json.Formatting.Indented);



    }


}
