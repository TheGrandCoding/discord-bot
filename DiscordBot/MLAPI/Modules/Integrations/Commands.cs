using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using DiscordBot.Commands;
using DiscordBot.Utils;
using Google.Cloud.Translation.V2;
using System;
using System.Collections.Generic;
using System.Linq;
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

        [Id(790917803087495168)]
        public async Task Ephemeral(string message = null)
        {
            await ReplyAsync(message == null ? "Test ephemeral message" : message, flags: InteractionResponseFlags.Ephemeral);
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

        [Id(790929406008098836)]
        [Id(790933585812652042)] // global
        public async Task Translate(string message, string from = null)
        {
            string fromLanguage = null;
            if(from != null)
            {
                fromLanguage = LanguageCodesUtils.ToCode(from);
                if(fromLanguage == null)
                {
                    await ReplyAsync($":x: Language not recognised.", flags: InteractionResponseFlags.Ephemeral);
                    return;
                }
            }
            var client = TranslationClient.Create();
            var response = await client.TranslateTextAsync(message, LanguageCodes.English, fromLanguage);
            var actualFrom = response.DetectedSourceLanguage == null ? fromLanguage : response.DetectedSourceLanguage;
            var name = LanguageCodesUtils.ToName(actualFrom);
            await ReplyAsync($"Translated from {name}:\r\n>>> {response.TranslatedText}", flags: InteractionResponseFlags.Ephemeral);
        }

    }
}
