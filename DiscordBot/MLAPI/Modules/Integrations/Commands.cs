using Discord.Addons.Interactive;
using Discord.Commands;
using DiscordBot.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.Integrations
{
    public class Commands : InteractionBase
    {
        public Commands(InteractionCommandContext context) : base(context)
        {
        }

        [Id(790595185910087722)]
        public async Task Ping(string message = null)
        {
            Program.LogMsg($"Withing ping, msg: {message}");
            await ReplyAsync("Heyo!");
        }

    }
}
