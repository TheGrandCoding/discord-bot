using Discord.WebSocket;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Classes.Rules;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.Guild
{
    public class GuildModule : AuthedAPIBase
    {
        public GuildModule(APIContext context) : base(context, "server")
        {
        }

        [Method("GET"), Path(@"/server/{guildId}/rules")]
        [Regex("guildId", @"[0-9]{17,18}")]
        public async Task Rules(ulong guildId)
        {
            SocketGuild guild = Program.Client.GetGuild(guildId);
            if(guild == null)
            {
                await RespondRaw("Unknown guild", 404);
                return;
            }
            var user = guild.GetUser(Context.User.Id);
            if(user == null)
            {
                await RespondRaw("Unknown guild", 400);
                return;
            }
            if(!(user.GuildPermissions.Administrator || user.GuildPermissions.ManageGuild))
            {
                await RespondRaw("Forbidden", 403);
                return;
            }
            var service = Context.Services.GetRequiredService<RulesService>();
            if(!service.Rules.TryGetValue(guildId, out var rules))
            {
                await RespondRaw("You need to setup the rules in the first instance via command first. You can edit them here later. Yes - I'm lazy.", 400);
                return;
            }
            var table = new Table(id: "table")
                .WithHeaderColumn("Rule #")
                .WithHeaderColumn("Short Title")
                .WithHeaderColumn("Long Description");
            foreach (var rule in rules.CurrentRules)
            {
                var tr = new TableRow(cls: "ruleItem")
                {
                    Children =
                    {
                        new TableData("")
                        {
                            Children =
                            {
                                new Input("number", rule.Id.ToString())
                            }
                        },
                        new TableData("")
                        {
                            Children =
                            {
                                new Input("text", rule.Short)
                            }
                        },
                        new TableData("")
                        {
                            Children =
                            {
                                new RawObject($"<textarea>{rule.Long}</textarea>")
                            }
                        }
                    }
                };
                table.Children.Add(tr);
            }
            table.Children.Add(new TableRow(id: "addNewBtn")
            {
                Children =
                {
                    new TableData("")
                    {
                        Children =
                        {
                            new Input("button", "Add New Rule")
                            {
                                Style = "width: 100%; height: 100%",
                                OnClick = "newRule();"
                            }
                        }
                    }.WithTag("colspan", "3")
                }
            });
            await ReplyFile("rules.html", 200,
                new Replacements()
                .Add("server", guild)
                .Add("table", table));
        }

        [Method("POST"), Path(@"/server/{guildId}/rules")]
        [Regex("guildId", @"[0-9]{17,18}")]
        public async Task SetRules(ulong guildId)
        {
            SocketGuild guild = Program.Client.GetGuild(guildId);
            if (guild == null)
            {
                await RespondRaw("Unknown guild", 404);
                return;
            }
            var user = guild.GetUser(Context.User.Id);
            if (user == null)
            {
                await RespondRaw("Unknown guild", 400);
                return;
            }
            if (!(user.GuildPermissions.Administrator || user.GuildPermissions.ManageGuild))
            {
                await RespondRaw("Forbidden", 403);
                return;
            }
            var service = Context.Services.GetRequiredService<RulesService>();
            if (!service.Rules.TryGetValue(guildId, out var ruleset))
            {
                await RespondRaw("You need to setup the rules by command first", 400);
                return;
            }
            var jarray = JArray.Parse(Context.Body);
            var rules = new List<ServerRule>();
            foreach (JObject obj in jarray)
            {
                var rule = new ServerRule();
                rule.Id = obj["id"].ToObject<int>();
                rule.Short = obj["short"].ToObject<string>();
                rule.Long = obj["long"].ToObject<string>();
                if(rule.Short.Length > 256 || rule.Long.Length > 1024)
                {
                    await RespondRaw($"Item {rule.Id} has invalid properties", 400);
                    return;
                }
                rules.Add(rule);
            }
            ruleset.CurrentRules = rules;
            service.Update(ruleset).Wait();
            service.OnSave();
            await RespondRaw("OK", 200);
        }
    }
}
