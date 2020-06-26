using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Classes.ServerList;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.ServerList
{
    [RequireAuthentication(false)]
    public class MLServers : APIBase
    {
        public MLServers(APIContext c) : base(c, "masterlist")
        {
            Service = Program.Services.GetRequiredService<MLService>();
        }
        public MLService Service { get; set; }

        public override void BeforeExecute()
        {
            if (!Service.Lock.WaitOne(5000))
                throw new HaltExecutionException($"Could not achieve thread safe lock");
        }
        public override void AfterExecute()
        {
            if (Context.Method != "GET")
                Service.OnSave();
            Service.Lock.Release();
        }

        #region Browser Visible
        [Method("GET"), Path("/masterlist")]
        public void Servers(string game = null)
        {
            var table = new Table()
            {
                Children =
                {
                    new TableRow()
                    {
                        Children =
                        {
                            new TableHeader("Name"),
                            new TableHeader("Game"),
                            new TableHeader("Players")
                        }
                    }
                }
            };
            foreach(var x in Service.Servers.Values)
            {
                if (game != null && x.GameName != game)
                    continue;
                var row = new TableRow(x.Id.ToString())
                {
                    Children =
                    {
                        new TableData(null) { Children = { new Anchor("/masterlist/" + x.Id, x.Name) } },
                        new TableData(null) { Children = { new Anchor($"/masterlist?game={x.GameName}", x.GameName) } },
                        new TableData(x.Players.Count.ToString())
                    }
                };
                table.Children.Add(row);
            }
            ReplyFile("base.html", HttpStatusCode.OK, new Replacements()
                .Add("table", table.ToString())
                .Add("count", Service.Servers.Count));
        }

        string getConnectionInfo(Server server)
        {
            string ip = "[withheld]";
            if(server.ExternalIP.Equals(Context.Request.RemoteEndPoint.Address))
                ip = server.InternalIP.ToString();
            else if (!server.IsPrivate)
                ip = server.ExternalIP.ToString();
            return $"<p>Connection IP: <strong>{ip}</strong></p>";
        }

        [Method("GET"), PathRegex(@"\/masterlist\/(?!.*\/.)(?<id>[a-zA-Z0-9-]+)", "/masterlist/<id>")]
        public void SeeSpecificServer(Guid id)
        {
            if(!Service.Servers.TryGetValue(id, out var server))
            {
                HTTPError(HttpStatusCode.NotFound, "No server", "Could not find a server by that Id.");
                return;
            }
            var table = new Table()
            {
                Children =
                {
                    new TableRow()
                    {
                        Children =
                        {
                            new TableHeader("Name"),
                            new TableHeader("Score"),
                            new TableHeader("Latency")
                        }
                    }
                }
            };
            foreach(var player in server.Players.OrderByDescending(x => x.Score))
            {
                var row = new TableRow()
                {
                    Children =
                    {
                        new TableData(player.Name),
                        new TableData(player.Score.ToString()),
                        new TableData(player.Latency.ToString())
                    }
                };
                table.Children.Add(row);
            }
            ReplyFile("server.html", HttpStatusCode.OK, new Replacements()
                .Add("server", server)
                .Add("players", table)
                .Add("connection", getConnectionInfo(server)));
        }
        #endregion

        #region API Endpoints

        bool testPortForward(IPAddress ip, int port)
        {
            using TcpClient client = new TcpClient(ip.AddressFamily);
            Task task;
            try
            {
                task = client.ConnectAsync(ip, port);
                task.Wait(5000);
            } catch
            {
                return false;
            }
            if(task.IsCompleted)
                return client.Connected; // should dispose/close stream.
            return false;
        }

        #region Server API
        [Method("POST"), Path("/servers")]
        public void CreateServer(string name, string type, int port, string internalIP, bool skipPort = false)
        {
            if(!IPAddress.TryParse(internalIP, out var intIP))
            {
                RespondRaw($"Internal IP not in proper format.", 400);
                return;
            }
            var existing = Service.Servers.Values.Any(x => x.Name == name && x.GameName == type);
            if(existing)
            {
                RespondRaw($"Existing server already exists with that name and type", HttpStatusCode.Conflict);
                return;
            }
            var externalIP = Context.Request.RemoteEndPoint.Address;
            var portForwarded = skipPort || testPortForward(externalIP, port);
            var srv = new Server(name, type, intIP, externalIP, port);
            srv.Id = Guid.NewGuid();
            Service.Servers[srv.Id] = srv;
            Service.OnSave();
            RespondRaw(Program.Serialise(srv), HttpStatusCode.Created);
        }
        
        [Method("PATCH"), PathRegex(@"\/servers\/(?!.*\/.)(?<id>[a-zA-Z0-9-]+)")]
        public void PatchServer(Guid id, string auth, int? port = 0, string internalIP = null)
        {
            if(!Service.Servers.TryGetValue(id, out var server))
            {
                RespondRaw("Unknown server", 404);
                return;
            }
            if(server.Authentication != auth)
            {
                RespondRaw("Unknown server", 404);
                return;
            }
            IPAddress intIP = null;
            if(internalIP != null && !IPAddress.TryParse(internalIP, out intIP))
            {
                RespondRaw("Could not parse internal IP", 400);
                return;
            }
            server.Port = port ?? server.Port;
            server.InternalIP = intIP ?? server.InternalIP;
            server.LastDateOnline = DateTime.Now;
            var extIP = Context.Request.RemoteEndPoint.Address;
            if(extIP != server.ExternalIP)
            {
                server.ExternalIP = extIP;
                server.PortForwarded = testPortForward(extIP, server.Port);
            }
            RespondRaw(Program.Serialise(server), HttpStatusCode.OK);
        }
        
        [Method("POST"), PathRegex(@"\/servers\/(?!.*\/.)(?<id>[a-zA-Z0-9-]+)")]
        public void ServerInfo(Guid id, string auth)
        {
            if (!Service.Servers.TryGetValue(id, out var server))
            {
                RespondRaw("Unknown server", 404);
                return;
            }
            if (server.Authentication != auth)
            {
                RespondRaw("Unknown server", 404);
                return;
            }
            server.LastDateOnline = DateTime.Now;
            RespondRaw("Ok", HttpStatusCode.OK);
        }

        [Method("DELETE"), PathRegex(@"\/servers\/(?!.*\/.)(?<id>[a-zA-Z0-9-]+)")]
        public void DeleteServer(Guid id, string auth)
        {
            if (!Service.Servers.TryGetValue(id, out var server))
            {
                RespondRaw("Unknown server", 404);
                return;
            }
            if (server.Authentication != auth)
            {
                RespondRaw("Unknown server", 404);
                return;
            }
            Service.Servers.Remove(id);
            RespondRaw($"Removed", HttpStatusCode.NoContent);
        }
        #endregion

        #region Client API
        [Method("GET"), Path("/servers")]
        public void GetServers(string type, bool showOffline = false)
        {
            var sb = new StringBuilder("["); // manually build the json
            foreach(var server in Service.Servers.Values)
            {
                if (server.Online == false && showOffline == false)
                    continue;
                if (server.GameName != type)
                    continue;
                if(!server.PortForwarded)
                {
                    if (!Context.Request.RemoteEndPoint.Address.Equals(server.ExternalIP))
                        continue;
                }
                if (server.IsPrivate)
                    continue;
                sb.Append(server.ToJson());
                sb.Append(",");
            }
            if (sb.Length > 1)
                sb.Remove(sb.Length - 1, 1); // strip leading ,
            sb.Append("]");
            RespondRaw(sb.ToString(), 200);
        }
        #endregion

        #endregion
    }
}
