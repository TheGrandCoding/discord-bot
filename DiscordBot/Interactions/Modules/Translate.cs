using Discord;
using Discord.Interactions;
using DiscordBot.Utils;
using Google.Cloud.Translation.V2;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Modules
{
    public class Translate : BotSlashBase
    {
        [SlashCommand("translate", "Translates the provided text into English")]
        public async Task TranslateCmd(string message, string language = null) 
        {
            string fromLanguage = null;
            if (language != null)
            {
                fromLanguage = LanguageCodesUtils.ToCode(language);
                if (fromLanguage == null)
                {
                    await RespondAsync(":x: Language not recognised", ephemeral: true, embeds: null);
                    return;
                }
            }
            await DeferAsync(true);
            var client = TranslationClient.Create();
            var response = await client.TranslateTextAsync(message, LanguageCodes.English, fromLanguage);
            var actualFrom = response.DetectedSourceLanguage == null ? fromLanguage : response.DetectedSourceLanguage;
            var name = LanguageCodesUtils.ToName(actualFrom);
            var embed = new EmbedBuilder();
            embed.Title = "Translated from " + name;
            embed.Description = response.TranslatedText;
            await FollowupAsync(embeds: new[] { embed.Build() });
        }
    }
}
