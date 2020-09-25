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
    }
}
