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
            var sonarrEvent = JsonConvert.DeserializeObject<SonarrEvent>(Context.Body);
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
