using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    [RequireVerifiedAccount]
    public class Legislation : AuthedAPIBase
    {
        public LegislationService Service { get; set; }
        public Legislation(APIContext context) : base(context, "laws") 
        {
            Service = Program.Services.GetRequiredService<LegislationService>();
        }

        [Method("GET")]
        [Path("/laws/{name}")]
        [Regex(".", @"\/laws\/(?<path>[a-z0-9-\/]+)")]
        public void SeeLaw(string path, bool raw = false)
        {
            if(!Service.Laws.TryGetValue(path, out var act))
            {
                var anyBegin = Service.Laws.Keys.Where(x => x.StartsWith(path)).ToList();
                if(anyBegin.Count > 0)
                {
                    var table = new Table();
                    table.WithHeaderColumn("Short Title");
                    table.WithHeaderColumn("Long Title");
                    table.WithHeaderColumn("Enacted");
                    foreach (var x in anyBegin)
                    {
                        var _a = Service.Laws[x];
                        table.WithRow(
                                new Anchor($"/laws/{x}", _a.ShortTitle),
                                _a.LongTitle,
                                _a.EnactedDate.HasValue ? _a.EnactedDate.Value.ToLongDateString() : "Not yet enacted"
                            );
                    }
                    RespondRaw($"<!DOCTYPE html><html><head></head><body>{table}</body></html>", 200);
                }
                else
                {
                    HTTPError(HttpStatusCode.NotFound, "", "No law by that path name");
                }
                return;
            }
            var page = LegislationService.PageForAct(act, raw);
            RespondRaw(ReplaceMatches(page, new Replacements()), HttpStatusCode.OK);
        }
    }
}
