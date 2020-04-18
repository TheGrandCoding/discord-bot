using Discord.Commands;
using DiscordBot.Commands;
using DiscordBot.Commands.Attributes;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    [Summary("Testing Commands")]
    public class Testing : BotModule
    {
        [Command("ping")]
        public async Task Ping()
        {
            await ReplyAsync("Pong!");
        }

        [Command("error")]
        public async Task Error()
        {
            throw new InvalidOperationException("Failed to do stuff!");
        }

        [Command("thing")]
        public async Task Arguments(int c)
        {
            await ReplyAsync($"**{c}**");
        }
    }
}
