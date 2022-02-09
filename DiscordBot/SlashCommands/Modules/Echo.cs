﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [Group("echo", "Repeats stuff")]
    public class Echo : BotSlashBase
    {
        [SlashCommand("text", "Repeats what you send")]
        public async Task EchoCmd(string text)
        {
            await RespondAsync(text, ephemeral: true, embeds: null);
        }

        [SlashCommand("embed", "Repeats what you send, but fancy")]
        public async Task EmbedEcho(string text, bool ephemeral = true)
        {
            await RespondAsync("Response:", embeds: new[] { new EmbedBuilder()
                .WithTitle("Echo")
                .WithAuthor(Context.User)
                .WithDescription(text).Build() },
                ephemeral: ephemeral
                );
        }

        [SlashCommand("buttons", "Test command for buttons")]
        public async Task Buttons()
        {
            try
            {
                ComponentBuilder builder = new ComponentBuilder();
                builder.WithButton("Test button 1", "btn1", ButtonStyle.Danger);
                builder.WithButton("Test button 2", "btn2", ButtonStyle.Danger);
                await RespondAsync("Click below", components: builder.Build(), embeds: null);
                var msg = await GetOriginalResponseAsync();
                var srv = Program.Services.GetRequiredService<MessageComponentService>();
                srv.Register(msg, handleButton);
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "EchoButtons");
            }
        }

        public static async Task handleButton(CallbackEventArgs args)
        {
            var token = args.Interaction;
            await token.UpdateAsync(x =>
            {
                x.Content = $"Clicked {args.ComponentId}";
            });
        }
    }
}
