using Discord;
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
            await ReplyAsync(message == null ? "Pong!" : message);
        }

        [Id(790593912271273995)]
        public async Task Test()
        {
            await ReplyAsync("Test command success", embed: new EmbedBuilder()
                .WithTitle("Hello!")
                .WithDescription("What even means this")
                .WithColor(Color.Green)
                .Build());
        }

    }
}
