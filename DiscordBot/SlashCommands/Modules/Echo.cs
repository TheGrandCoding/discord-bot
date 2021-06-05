using Discord;
using Discord.SlashCommands;
using Discord.WebSocket;
using DiscordBot.Services.BuiltIn;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [CommandGroup("echo")]
    public class Echo : BotSlashBase
    {
        [SlashCommand("text", "Repeats what you send")]
        public async Task EchoCmd([Required]string text)
        {
            await Interaction.RespondAsync(text, ephemeral: true);
        }

        [SlashCommand("embed", "Repeats what you send, but fancy")]
        public async Task EmbedEcho([Required]string text, bool ephemeral = true)
        {
            await Interaction.RespondAsync("Response:", embed: new EmbedBuilder()
                .WithTitle("Echo")
                .WithAuthor(Interaction.User)
                .WithDescription(text).Build(),
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
                var msg = await Interaction.RespondAsync("Click below", component: builder.Build());
                var srv = Program.Services.GetRequiredService<DiscordBot.Services.BuiltIn.MessageComponentService>();
                srv.Register(msg, handleButton);
            }
            catch (Exception ex)
            {
                Program.LogMsg(ex, "EchoButtons");
            }
        }

        public static void handleButton(object sender, CallbackEventArgs args)
        {
            var token = args.Interaction;
            token.RespondAsync(text: $"Clicked {args.ComponentId}", type: InteractionResponseType.UpdateMessage);
        }
    }
}
