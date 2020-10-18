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
    public class PermChanges : BotModule
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

        [Command("list")]
        public async Task List()
        {
            var embed = new EmbedBuilder();
            foreach(var x in Service.Situations)
            {
                var field = new EmbedFieldBuilder();
                field.Name = $"<@${x.Key}>";
                field.Value = "";
                foreach(var action in x.Value)
                {
                    var crit = action.Value.State.HasValue ? "when " + action.Value.State.ToString() : "";
                    var add = action.Value.PermAdded == null ? "" : "add `" + action.Value.PermAdded + "`";
                    var rem = action.Value.PermRemoved == null ? "" : "rem `" + action.Value.PermRemoved + "`";
                    field.Value += $"{action.Key}: {crit} {add} {rem}\r\n";
                }
                if ((string)field.Value == "")
                    field.Value = "No actions.";
                embed.AddField(field);
            }
            await ReplyAsync(embed: embed.Build());
        }
    }
}
