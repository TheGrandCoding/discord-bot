using DiscordBot.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace DiscordBot.MLAPI.Modules.Bot
{
    
    public class Internal : APIBase
    {
        public Internal(APIContext c) : base(c, "/") { }

        [Method("GET"), Path("/bot/restart")]
        [RequireOwner]
        public void RestartBot()
        {
            Program.LogMsg("Starting to restart due to request", Discord.LogSeverity.Critical, "Internal");
            Program.Save(true);
            Service.SendClose();
            RespondRaw("OK", 200);
#if LINUX
            ProcessStartInfo Info = new ProcessStartInfo();
            Info.Arguments = "-c ping 127.0.0.1 -c 2 && /home/pi/Desktop/runasbot.sh new";
            //Info.WindowStyle = ProcessWindowStyle.Normal;
            //Info.CreateNoWindow = true;
            Info.FileName = "/bin/bash";
            Process.Start(Info);
#endif
            Environment.Exit(0);
        }

        [Method("POST"), Path("/bot/build")]
        [RequireAuthentication(false)]
        public void GithubWebhook()
        {
            string value = Context.HTTP.Request.Headers["Authorization"];
            var bytes = Convert.FromBase64String(value.Split(' ')[1]);
            var combined = Encoding.UTF8.GetString(bytes);
            var password = combined.Split(':')[1];
            if(password == Program.Configuration["tokens:github:internal"])
            {
                Program.LogMsg("Restarting but due to GitHub push.", Discord.LogSeverity.Warning, "Internal");
                RestartBot();
            } else
            {
                RespondRaw("Failed.", 400);
            }
        }
    }
}
