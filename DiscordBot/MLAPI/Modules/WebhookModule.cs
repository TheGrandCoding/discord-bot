using DiscordBot.Commands.Modules.MLAPI;
using DiscordBot.Services.Sonarr;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    [RequireApproval(false)]
    [RequireAuthentication(false)]
    public class WebhookModule : APIBase
    {
        public WebhookModule(APIContext context) : base(context, "webhooks")
        {
        }

        [Method("POST"), Path("webhooks/sonarr")]
        public void Sonarr()
        {
            var jobj = JObject.Parse(Context.Body);
            var srv = Program.Services.GetRequiredService<SonarrWebhooksService>();
            srv.Handle(jobj);
            RespondRaw("OK");
        }
    }
}
