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

        [Method("GET"), Path("/bot/close")]
        [RequireOwner]
        public void CloseBot()
        {
            RespondRaw("OK");
            Program.Close(0);
        }

        [Method("GET"), Path("/bot/restart")]
        [RequireOwner]
        public void RestartBot()
        {
            RespondRaw("OK");
            Program.Close(1);
        }


        [Method("POST"), Path("/bot/build")]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        [RequireGithubSignatureValid("bot:build")]
        public void GithubWebhook()
        {
            Program.Close(69); // closing with a non-zero code restarts it.
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
                RespondRaw("OK", 200);
            }
            else
            {
                RespondRaw("Failed.", 400);
            }
        }
    }
}
