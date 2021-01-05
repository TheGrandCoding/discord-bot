using Discord;
using Discord.Commands;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("event")]
    public class PermChanges : BotBase
    {
        public PermSituationService Service { get; set; }

        [Command("add")]
        public async Task Add(string eventName, string add, string remove, ulong? criteria = null)
        {
            var situation = new Situation()
            {
                Event = eventName,
                PermAdded = add == "-" ? null : add,
                PermRemoved = remove == "-" ? null : remove,
                State = criteria
            };
            Service.Register(Context.Guild, situation.Event, situation.PermAdded, situation.PermRemoved, situation.State);
            await ReplyAsync("Added.");
        }

        string getActions(Services.Situation action, string newline = "\r\n")
        {
            var add = action.PermAdded == null ? "" : "+`" + action.PermAdded + "`";
            var rem = action.PermRemoved == null ? "" : "-`" + action.PermRemoved + "`";
            var total = add + newline + rem;
            if (total == newline)
                return newline + "no actions";
            return total;
        }

        [Command("list")]
        public async Task List()
        {
            var embed = new EmbedBuilder();
            foreach(var x in Service.Situations)
            {
                var guild = Program.Client.GetGuild(x.Key);
                var roles = new Dictionary<ulong, List<Services.Situation>>();
                foreach(var action in x.Value)
                {
                    if(action.Value.Event.StartsWith("Role"))
                    {
                        if (roles.TryGetValue(action.Value.State.Value, out var ls))
                            ls.Add(action.Value);
                        else
                            roles[action.Value.State.Value] = new List<Services.Situation>() { action.Value };
                    } else
                    {
                        var field = new EmbedFieldBuilder();
                        field.Name = $"On {action.Value.Event}";
                        field.Value = getActions(action.Value);
                        embed.AddField(field);
                    }
                }
                foreach(var rolePair in roles)
                {
                    var field = new EmbedFieldBuilder();
                    var role = guild.GetRole(rolePair.Key);
                    field.Name = $"{role.Name}";
                    string s = "";
                    foreach(var situation in rolePair.Value)
                    {
                        s += "when " + situation.Event[4..] + ":\r\n  ";
                        s += getActions(situation, "\r\n  ");
                    }
                    field.Value = s;
                    embed.AddField(field);
                }
            }
            await ReplyAsync(embed: embed.Build());
        }
    }
}
