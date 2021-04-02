using DiscordBot.Commands.Modules.MLAPI;
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
            Program.LogMsg($"Received webhook", Discord.LogSeverity.Info, "OnGrab");
            var sonarrEvent = JsonConvert.DeserializeObject<SonarrEvent>(Context.Body);
            Program.LogMsg($"Parsed {sonarrEvent.EventType}", Discord.LogSeverity.Info, "OnGrab");
            try
            {
                var towrite = JsonConvert.SerializeObject(sonarrEvent, Formatting.Indented);
                File.WriteAllText("latest" + sonarrEvent.EventType + ".json", towrite);
            } catch { }
            var srv = Program.Services.GetRequiredService<SonarrWebhooksService>();
            srv.Handle(sonarrEvent);
            RespondRaw("OK");
        }
    }
}
