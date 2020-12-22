using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using DiscordBot.Commands;
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

        string getCode(string incoming)
        {
            var constants = typeof(LanguageCodes).GetFields()
                .Where(x => x.FieldType == typeof(string));
            var nameToCode = new Dictionary<string, string>();
            var codeToName = new Dictionary<string, string>();
            foreach(var constant in constants)
            {
                var name = constant.Name;
                var code = constant.GetValue(null);
                nameToCode[name.ToLower()] = code as string;
                codeToName[code as string] = name;
            }
            if (nameToCode.TryGetValue(incoming.ToLower(), out var val))
                return val;
            if (codeToName.ContainsKey(incoming))
                return incoming;
            return null;
        }

        [Id(790929406008098836)]
        public async Task Translate(string message, string from = null)
        {
            string fromLanguage = null;
            if(from != null)
            {
                fromLanguage = getCode(from);
                if(fromLanguage == null)
                {
                    await ReplyAsync($":x: Language not recognised.", flags: InteractionResponseFlags.Ephemeral);
                    return;
                }
            }
            var client = TranslationClient.Create();
            var response = await client.TranslateTextAsync(message, LanguageCodes.English, fromLanguage);
            var actualFrom = response.DetectedSourceLanguage == null ? fromLanguage : response.DetectedSourceLanguage;
            await ReplyAsync($"Translated from {actualFrom}:\r\n>>> {response.TranslatedText}", flags: InteractionResponseFlags.Ephemeral);
        }

    }
}
