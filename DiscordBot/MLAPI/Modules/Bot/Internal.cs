using Discord;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.Bot
{
    
    public class Internal : APIBase
    {
        public Internal(APIContext c) : base(c, "/bot") 
        {
        }

        [Method("GET"), Path("/bot/close")]
        [RequireOwner]
        public async Task CloseBot()
        {
            RespondRaw("OK");
            Program.Close(0);
        }

        [Method("GET"), Path("/bot/restart")]
        [RequireOwner]
        public async Task RestartBot()
        {
            RespondRaw("OK");
            Program.Close(1);
        }


        [Method("POST"), Path("/bot/build")]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        [RequireGithubSignatureValid("bot:build")]
        public async Task GithubWebhook()
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
        public async Task NEAWebhook()
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


        public static ShutdownState shutdownState = ShutdownState.Running;
        public static string failOrWaitReason = null;

        public static ComponentBuilder getShutdownComponents()
        {
            var select = new SelectMenuBuilder();
            select.CustomId = "internal:shutdown";
            select.AddOption("Running", $"{(int)ShutdownState.Running}", "There is no request", isDefault: shutdownState == ShutdownState.Running);
            select.AddOption("Requested", $"{(int)ShutdownState.Requested}", "A request has been sent, not acked", isDefault: shutdownState == ShutdownState.Requested);
            select.AddOption("In Progress", $"{(int)ShutdownState.InProgress}", "The request is in progress", isDefault: shutdownState == ShutdownState.InProgress);
            select.AddOption("Waiting", $"{(int)ShutdownState.Waiting}", "In process, but long time", isDefault: shutdownState == ShutdownState.Waiting);
            select.AddOption("Failed or Refused", $"{(int)ShutdownState.Failed}", "Request failed or was refused", isDefault: shutdownState == ShutdownState.Failed);
            select.AddOption("Completed", $"{(int)ShutdownState.Completed}", "The computer is shutdown", isDefault: shutdownState == ShutdownState.Completed);

            return new ComponentBuilder()
                .WithSelectMenu(select);
        }

        [Method("GET"), Path("/pc/shutdown")]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        public async Task RequestPcShutdown()
        {
            if(shutdownState == ShutdownState.Running)
            {
                failOrWaitReason = null;
                shutdownState = ShutdownState.Requested;
                var x = Program.AppInfo.Owner.SendMessageAsync(embed:
                    new EmbedBuilder()
                    .WithTitle("Request to shutdown PC")
                    .AddField("IP", Context.IP, true)
                    .AddField("User-Agent", Context.Request.UserAgent ?? "n/a", true)
                    .WithFooter($"Use {Program.Prefix}bot pc_reason [text] to set a reason for wait or refusal")
                    .Build(),
                    components: getShutdownComponents().Build()).Result;

                ReplyFile("shutdown.html", 200, new Replacements()
                    .Add("text", "A request to gracefully shutdown the computer has been sent.<br/>" +
                    "<h1>Do not turn off the computer until the request has been completed</h1> " +
                    "Doing so could cause irreversible damage to programs still running.")
                    .Add("doReload", "true"));
            } else if (shutdownState == ShutdownState.Requested)
            {
                ReplyFile("shutdown.html", 200, new Replacements()
                    .Add("text", "A request to gracefully shutdown the computer has been sent. " +
                    "<h1>Do not turn off the computer until the request has been completed</h1> " +
                    "Doing so could cause irreversible damage to programs still running.")
                    .Add("doReload", "true"));
            } else if (shutdownState == ShutdownState.InProgress)
            {
                ReplyFile("shutdown.html", 200, new Replacements()
                    .Add("text", "The request is now <em>in progress</em>. Please stand by. " +
                    "<strong>Do not turn off the computer yet</strong> " +
                    "Doing so could cause irreversible damage to programs still running.")
                    .Add("doReload", "true"));
            } else if(shutdownState == ShutdownState.Waiting)
            {
                ReplyFile("shutdown.html", 200, new Replacements()
                    .Add("text", "<strong>Your request is still being processed</strong>, but is taking longer than usual. <br/>" +
                    (failOrWaitReason == null 
                        ? "This could be because programs are still being closed, or because the computer is not accessible to the internet.<br/>"
                        : $"This is because <em>{failOrWaitReason}</em>") +
                    "<h1>Please do not turn off the computer</h1>")
                    .Add("doReload", "true"));
            } else if (shutdownState == ShutdownState.Failed)
            {
                ReplyFile("shutdown.html", 200, new Replacements()
                    .Add("text", "<strong>Your request has failed or was refused</strong><br/>" +
                    (failOrWaitReason == null
                        ? "This could be because programs are actively still in use or because the computer is not accessible to the internet.<br/>"
                        : $"This is because <em>{failOrWaitReason}</em>") +
                    "<h1>Please do not turn off the computer</h1>")
                    .Add("doReload", "false"));
            }
            else if(shutdownState == ShutdownState.Completed)
            {
                ReplyFile("shutdown.html", 200, new Replacements()
                    .Add("text", "The request has been completed. The computer should now, or will soon, be offline.")
                    .Add("doReload", "false"));
            }
        }

        public enum ShutdownState
        {
            Running,
            Requested,


            InProgress,
            Waiting,

            Failed,
            Completed
        }
    }
}
