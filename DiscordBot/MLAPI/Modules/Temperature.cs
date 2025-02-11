using Microsoft.Extensions.Configuration;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{
    [Path("/temps")]
    public class Temperature : AuthedAPIBase
    {
        private readonly IConfiguration _config;
        public Temperature(APIContext context) : base(context, "temps")
        {
            _config = Program.Configuration.GetSection("tokens:temperature");
        }

        [Method("GET"), Path("/")]
        public Task Index()
        {
            return ReplyFile("index.html", 200);
        }

        Task sendFile(string path)
        {
            var key = new Renci.SshNet.PrivateKeyFile(_config["key"]);
            using var scp = new ScpClient(_config["host"], _config["user"], key);
            scp.Connect();

            StatusSent = 200;
            Context.HTTP.Response.StatusCode = 200;
            scp.Download(path, Context.HTTP.Response.OutputStream);
            Context.HTTP.Response.Close();
            return Task.CompletedTask;
        }

        [Method("GET"), Path("/api/readings/{date}")]
        [Regex("date", @"[0-9]{4}-[0-9]{2}-[0-9]{2}")]
        public Task ApiGetTemps(string date)
            => sendFile(string.Format(_config["dlpath"], date));

        [Method("GET"), Path("/api/settings")]
        public Task ApiGetHeatings() => sendFile(string.Format(_config["dlpath"], "settings"));
    }
}
