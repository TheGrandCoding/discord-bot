using Discord;
using DiscordBot.Utils;
using Google.Cloud.Translation.V2;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class TranslationService : SavedService
    {
        public Dictionary<ulong, List<ulong>> Watched { get; set; }
        public override string GenerateSave()
        {
            return Program.Serialise(Watched);
        }
        public override void OnReady()
        {
            var sv = ReadSave();
            Watched = Program.Deserialise<Dictionary<ulong, List<ulong>>>(sv);
            Program.Client.MessageReceived += Client_MessageReceived;
        }

        private async Task Client_MessageReceived(Discord.WebSocket.SocketMessage arg)
        {
            if (arg.Author.IsBot || arg.Author.IsWebhook)
                return;
            if (!(Watched.TryGetValue(arg.Author.Id, out var users)))
                return;
            if(users.Count == 0)
            {
                Watched.Remove(arg.Author.Id);
                return;
            }
            if (string.IsNullOrWhiteSpace(arg.Content))
                return;
            var client = TranslationClient.Create();
            var response = await client.TranslateTextAsync(arg.Content, LanguageCodes.English);
            if(response.DetectedSourceLanguage != LanguageCodes.English)
            {
                var embedB = new EmbedBuilder();
                embedB.Title = $"Translation from {LanguageCodesUtils.ToName(response.DetectedSourceLanguage)}";
                embedB.Url = arg.GetJumpUrl();
                embedB.WithFooter(arg.Author.Username, arg.Author.GetAnyAvatarUrl());
                embedB.Description = response.TranslatedText;
                if(response.OriginalText.Length > 1024)
                {
                    embedB.AddField($"Original, part 1", Program.Clamp(response.OriginalText, 1024));
                    embedB.AddField($"Original, part 2", response.OriginalText.Substring(1025));
                } else
                {
                    embedB.AddField("Original", Program.Clamp(response.OriginalText, 1024));
                }
                var embed = embedB.Build();
                var rm = new List<ulong>();
                foreach(var id in users)
                {
                    var usr = Program.Client.GetUser(id);
                    if (usr == null)
                    {
                        rm.Add(id);
                        continue;
                    }
                    await usr.SendMessageAsync(embed: embed);
                }
                foreach (var x in rm)
                    users.Remove(x);
                if(rm.Count > 0)
                {
                    Watched[arg.Author.Id] = users;
                    this.OnSave();
                }
            }
        }
    }
}
