using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.ServerList;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;

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
            RespondRaw(builder.ToString());
        }
        #endregion

        #region API Endpoints

        #region Server API
        [Method("POST"), Path("/servers")]
        public void CreateServer(string name, string auth, string type, int port)
        {
            var srv = new Server()
            {
                Name = name,
                Authentication = auth,
                GameName = type,
                Players = new List<Player>(),
                Port = port
            };
            srv.Id = Guid.NewGuid();
            Service.Servers[srv.Id] = srv;
            Service.OnSave();
            RespondRaw(Program.Serialise(srv), HttpStatusCode.Created);
        }
        #endregion

        #endregion
    }
}
