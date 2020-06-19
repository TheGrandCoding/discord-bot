using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    [RequireVerifiedAccount]
    public class Legislation : APIBase
    {
        public LegislationService Service { get; set; }
        public Legislation(APIContext context) : base(context, "laws") 
        {
            Service = Program.Services.GetRequiredService<LegislationService>();
        }

        [Method("GET"), PathRegex(@"\/laws\/(?!.*\/.)(?<name>[a-z0-9-]+)", "/laws/<name>")]
        public void SeeLaw(string name, bool raw = false)
        {
            if(!Service.Laws.TryGetValue(name, out var act))
            {
                HTTPError(HttpStatusCode.NotFound, "", "No law by that path name");
                return;
            }
            var page = Service.PageForAct(act, raw);
            RespondRaw(ReplaceMatches(page, new Replacements()), HttpStatusCode.OK);
        }
    }
}
