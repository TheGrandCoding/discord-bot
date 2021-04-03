﻿using DiscordBot.Commands.Modules.MLAPI;
using DiscordBot.Services.Radarr;
using DiscordBot.Services.Sonarr;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    public class WebhookModule : APIBase
    {
        public WebhookModule(APIContext context) : base(context, "webhooks")
        {
        }

        [Method("POST"), Path("webhooks/sonarr")]
        public void Sonarr()
        {
            var srv = Program.Services.GetRequiredService<SonarrWebhooksService>();
            if(srv.HasFailed)
            {
                RespondRaw("Internal error occurer; service has failed.", 500);
                return;
            }
            Program.LogMsg($"Received webhook", Discord.LogSeverity.Info, "OnGrab");
            var sonarrEvent = JsonConvert.DeserializeObject<SonarrEvent>(Context.Body);
            Program.LogMsg($"Parsed {sonarrEvent.EventType}", Discord.LogSeverity.Info, "OnGrab");
            try
            {
                var towrite = JsonConvert.SerializeObject(sonarrEvent, Formatting.Indented);
                File.WriteAllText("latest" + sonarrEvent.EventType + ".json", towrite);
            } catch { }
            srv.Handle(sonarrEvent);
            RespondRaw("OK");
        }
    
        [Method("POST"), Path("webhooks/radarr")]
        public void Radarr()
        {
            var srv = Program.Services.GetRequiredService<RadarrWebhookService>();
            if (srv.HasFailed)
            {
                RespondRaw("Internal error occurer; service has failed.", 500);
                return;
            }
            Program.LogMsg($"Received webhook", Discord.LogSeverity.Info, "OnGrab");
            var radarrEvent = JsonConvert.DeserializeObject<RadarrEvent>(Context.Body);
            Program.LogMsg($"Parsed {radarrEvent.EventType}", Discord.LogSeverity.Info, "OnGrab");
            try
            {
                var towrite = JsonConvert.SerializeObject(radarrEvent, Formatting.Indented);
                File.WriteAllText("latest" + radarrEvent.EventType + ".json", towrite);
            }
            catch { }
            srv.Handle(radarrEvent);
            RespondRaw("OK");
        }
    }
}
