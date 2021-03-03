﻿using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
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

        [Method("GET"), Path("/bot/restart")]
        [RequireOwner]
        public void RestartBot()
        {
            Program.LogMsg("Starting to restart due to request", Discord.LogSeverity.Critical, "Internal");
            Program.Close(0);
            Thread.Sleep(2500);
#if LINUX
            Program.LogMsg("Running restart script...");
            ProcessStartInfo Info = new ProcessStartInfo();
            Info.Arguments = "-c \"sleep 5 && /home/pi/Desktop/runasbot.sh new\"";
            Info.WindowStyle = ProcessWindowStyle.Normal;
            Info.CreateNoWindow = false;
            Info.UseShellExecute = true;
            Info.FileName = "/bin/bash";
            var proc = Process.Start(Info);
            Program.LogMsg($"New instance running under PID {proc.Id}");
#endif
            Environment.Exit(0);
        }

        [Method("GET"), Path("/bot/close")]
        [RequireOwner]
        public void CloseBot()
        {
            RespondRaw("OK");
            Program.Close(0);
            Thread.Sleep(10_000);
            Environment.Exit(0);
        }


        [Method("POST"), Path("/bot/build")]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        [RequireGithubSignatureValid("bot:build")]
        public void GithubWebhook()
        {
            RestartBot();
        }

        static string Bash(string cmd)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }


        [Method("POST"), Path("/bot/nea")]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        public void NEAWebhook()
        {
            string value = Context.HTTP.Request.Headers["Authorization"];
            var bytes = Convert.FromBase64String(value.Split(' ')[1]);
            var combined = Encoding.UTF8.GetString(bytes);
            var password = combined.Split(':')[1];
            if (password == Program.Configuration["tokens:github:internal"])
            {
                Program.LogMsg("Updating NEA files...", Discord.LogSeverity.Warning, "NEA");
                Program.LogMsg(Bash("/bot/dl_nea.sh"), Discord.LogSeverity.Verbose, "NEA");
                RespondRaw("OK", 200);
            }
            else
            {
                RespondRaw("Failed.", 400);
            }
        }
    }
}
