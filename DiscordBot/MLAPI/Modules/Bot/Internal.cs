using DiscordBot.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.Bot
{
    
    public class Internal : APIBase
    {
        public Internal(APIContext c) : base(c, "/") { }

        static ManualResetEventSlim closeReset;
        [Method("GET"), Path("/bot/restart")]
        [RequireOwner]
        public void RestartBot()
        {
            Program.LogMsg("Starting to restart due to request", Discord.LogSeverity.Critical, "Internal");
            Program.Save(true);
            RespondRaw("OK", 200); // since we've acknowledged the reqeuest, we'll respond now.
            closeReset = new ManualResetEventSlim();
            var th = new Thread(() =>
            {
                Service.SendClose();
                closeReset.Set();
            });
            th.Start();
            if(!closeReset.Wait(10_000))
            {
                Program.LogMsg("OnClose did not complete in time! Hopefully won't break too much...", Discord.LogSeverity.Critical, "Internal");
            }
#if LINUX
            ProcessStartInfo Info = new ProcessStartInfo();
            Info.Arguments = "-c sleep 5 && /home/pi/Desktop/runasbot.sh new";
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
