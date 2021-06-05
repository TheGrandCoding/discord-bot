using Discord.SlashCommands;
using DiscordBot.Utils;
using Google.Cloud.Translation.V2;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    public class Translate : BotSlashBase
    {
        [SlashCommand("translate", "Translates the provided text into English")]
        public async Task TranslateCmd([Required]string message, string language = null) 
        {
            string fromLanguage = null;
            if (language != null)
            {
                fromLanguage = LanguageCodesUtils.ToCode(language);
                if (fromLanguage == null)
                {
                    await Interaction.RespondAsync(":x: Language not recognised", ephemeral: true);
                    return;
                }
            }
            await Interaction.AcknowledgeAsync(Discord.InteractionResponseFlags.Ephemeral);
            var client = TranslationClient.Create();
            var response = await client.TranslateTextAsync(message, LanguageCodes.English, fromLanguage);
            var actualFrom = response.DetectedSourceLanguage == null ? fromLanguage : response.DetectedSourceLanguage;
            var name = LanguageCodesUtils.ToName(actualFrom);
            await Interaction.FollowupAsync($"Translate from {name}\r\n>>> {response.TranslatedText}");
        }
    }
}
