using DiscordBot.Commands.Modules.MLAPI;
using DiscordBot.Services;
using DiscordBot.Services.Radarr;
using DiscordBot.Services.Sonarr;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{
    public class WebhookModule : APIBase
    {
        public WebhookModule(APIContext context) : base(context, "webhooks")
        {
        }

        [Method("POST"), Path("webhooks/sonarr")]
        public async Task Sonarr()
        {
            var srv = Context.Services.GetRequiredService<SonarrWebhooksService>();
            if(srv.HasFailed)
            {
                await RespondRaw("Internal error occurer; service has failed.", 500);
                return;
            }
            Program.LogInfo($"Received webhook", "SonarrPOST");
            var sonarrEvent = JsonConvert.DeserializeObject<SonarrEvent>(Context.Body);
            Program.LogInfo($"Parsed {sonarrEvent.EventType}", "SonarrPOST");
            try
            {
                var towrite = JsonConvert.SerializeObject(sonarrEvent, Formatting.Indented);
                File.WriteAllText("latest" + sonarrEvent.EventType + ".json", towrite);
            } catch { }
            srv.Handle(sonarrEvent);
            await RespondRaw("OK");
        }
    
        [Method("POST"), Path("webhooks/radarr")]
        public async Task Radarr()
        {
            var srv = Context.Services.GetRequiredService<RadarrWebhookService>();
            if (srv.HasFailed)
            {
                await RespondRaw("Internal error occurer; service has failed.", 500);
                return;
            }
            Program.LogInfo($"Received webhook", "RadarrPOST");
            var radarrEvent = JsonConvert.DeserializeObject<RadarrEvent>(Context.Body);
            Program.LogInfo($"Parsed {radarrEvent.EventType}", "RadarrPOST");
            try
            {
                var towrite = JsonConvert.SerializeObject(radarrEvent, Formatting.Indented);
                File.WriteAllText("latest" + radarrEvent.EventType + ".json", towrite);
            }
            catch { }
            srv.Handle(radarrEvent);
            await RespondRaw("OK");
        }
    
        [Method("POST"), Path("webhooks/gh-catch")]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
#if !DEBUG
        [RequireGithubSignatureValid("webhooks:gh-catch")]
#endif
        public async Task GithubCatch()
        {
            var srv = Context.Services.GetRequiredService<GithubTestService>();
            var data = new DiscordBot.Services.JsonWebhook()
            {
                EventName = Context.Headers["X-GitHub-Event"],
                HookId = Context.Headers["X-GitHub-Hook-ID"],
                JsonBody = Context.Body
            };
            srv.InboundWebhook(data);

            await RespondRaw("OK");
        }
    

        async Task _checkRestart(string serviceName, string shortLogName)
        {
            var jobj = JObject.Parse(Context.Body);
            if (!jobj.TryGetValue("ref", out var refT))
            {
                await RespondRaw("No 'ref' value?", 400);
                return;
            }
            var asS = refT.ToObject<string>();
            if (asS == "refs/heads/main" || asS == "refs/heads/master")
            {
                await RespondRaw("Restarting", 200);
                var psi = new ProcessStartInfo("sudo");
                psi.Arguments = $"systemctl restart {serviceName}";
                var x = Process.Start(psi);
                x.ErrorDataReceived += (sender, e) =>
                {
                    Program.LogError(e.Data, $"Restart{shortLogName}");
                };
                x.OutputDataReceived += (sender, e) =>
                {
                    Program.LogInfo(e.Data, $"Restart{shortLogName}");
                };
                x.WaitForExit();
            }
            else
            {
                await RespondRaw($"Not restarting for ref '{asS}'", 200);
            }
        }
    
        
        [Method("POST"), Path("webhooks/gh-flask")]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
#if !DEBUG
        [RequireGithubSignatureValid("webhooks:gh-flask")]
#endif
        public async Task GitHubFlask()
        {
            await _checkRestart("flaskr", "FL");
        }

        [Method("POST"), Path("webhooks/gh-mlapibot")]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
#if !DEBUG
        [RequireGithubSignatureValid("webhooks:gh-mlapibot")]
#endif
        public async Task GitHubMlapibot()
        {
            await _checkRestart("mlapibot", "ML");
        }


        [Method("POST"), Path("webhooks/ds-statuspage")]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        public async Task StatusPage()
        {
            var jobj = JObject.Parse(Context.Body);
            var reason = jobj.ContainsKey("incident") ? "incident new/update" : "component new/update";
            await Program.SendLogMessageAsync($"Status `{reason}` webhook received.");
            await RespondRaw("Ok.", 200);
        }
    }
}
