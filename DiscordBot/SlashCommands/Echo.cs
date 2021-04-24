﻿using Discord;
using Discord.SlashCommands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands
{
    [CommandGroup("echo")]
    public class Echo : BotSlashBase
    {
        [SlashCommand("text", "Repeats what you send")]
        public async Task EchoCmd(string text)
        {
            await Interaction.RespondAsync(text, flags: Discord.InteractionResponseFlags.Ephemeral);
        }

        [SlashCommand("embed", "Repeats what you send, but fancy")]
        public async Task EmbedEcho(string text, bool ephemeral = true)
        {
            await Interaction.RespondAsync("Response:", embed: new EmbedBuilder()
                .WithTitle("Echo")
                .WithAuthor(Interaction.Member)
                .WithDescription(text).Build(),
                flags: ephemeral ? InteractionResponseFlags.Ephemeral : InteractionResponseFlags.None
                );
        }
    }
}