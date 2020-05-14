using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.ServerList;
using DiscordBot.Services;
using Emgu.CV.CvEnum;
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
        [Method("GET"), Path("/servers/list")]
        public void Servers()
        {
            var builder = new StringBuilder();
            using(var table = new HTMLTable(builder))
            {
                using(var headers = table.AddHeaderRow())
                {
                    headers.AddHeaderCell("Name");
                    headers.AddHeaderCell("Game");
                    headers.AddHeaderCell("Players");
                }
                foreach(var x in Service.Servers.Values)
                {
                    using (var row = table.AddRow(x.Id.ToString()))
                    {
                        row.AddCell(x.Name);
                        row.AddCell(x.GameName);
                        row.AddCell($"{x.Players.Count}");
                    }
                }
            }
            ReplyFile("base.html", HttpStatusCode.OK, new Replacements()
                .Add("table", builder.ToString()));
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
        public void CreateServer(string name, string type, int port, string internalIP)
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
            var portForwarded = testPortForward(externalIP, port);
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
            var extIP = Context.Request.RemoteEndPoint.Address;
            if(extIP != server.ExternalIP)
            {
                server.ExternalIP = extIP;
                server.PortForwarded = testPortForward(extIP, server.Port);
            }
            RespondRaw(Program.Serialise(server), HttpStatusCode.OK);
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
