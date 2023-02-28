using Discord;
using Discord.WebSocket;
using DiscordBot.Classes.Attributes;
using DiscordBot.Utils;
using Google.Cloud.Translation.V2;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class TranslationService : SavedService, ISARProvider
    {
        public TranslationService()
        {
        }

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
            Program.Client.ReactionAdded += Client_ReactionAdded;
        }

        private async Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2, SocketReaction arg3)
        {
            var arg = new ReactionEventArgs()
            {
                Action = EventAction.Added,
                Emote = arg3.Emote,
                State = null,
                Message = await arg1.GetOrDownloadAsync(),
                User = arg3.User.GetValueOrDefault(null) ?? Program.Client.GetUser(arg3.UserId)
            };
            ReactionHandle(this, arg);
        }

        static string convertRegionalToLetter(string regionalIndicator)
        {
            switch(regionalIndicator)
            {
                case "🇦": return "a";
                case "🇧": return "b";
                case "🇨": return "c";
                case "🇩": return "d";
                case "🇪": return "e";
                case "🇫": return "f";
                case "🇬": return "g";
                case "🇭": return "h";
                case "🇮": return "i";
                case "🇯": return "j";
                case "🇰": return "k";
                case "🇱": return "l";
                case "🇲": return "m";
                case "🇳": return "n";
                case "🇴": return "o";
                case "🇵": return "p";
                case "🇶": return "q";
                case "🇷": return "r";
                case "🇸": return "s";
                case "🇹": return "t";
                case "🇺": return "u";
                case "🇻": return "v";
                case "🇼": return "w";
                case "🇽": return "x";
                case "🇾": return "y";
                case "🇿": return "z";
                default: return null;
            }
        }

        static string getLanguage(string code)
        {
            switch(code)
            {
                case "gb":
                case "us":
                    return "en";
                case "fr":
                case "de":
                    return code;
                default:
                    return null;
            }
        }

        public static async void ReactionHandle(object sender, ReactionEventArgs args)
        {
            var asLetters = new StringBuilder();
            if (args.Emote.Name.Length < 2)
                return;
            for(int i = 0; i < args.Emote.Name.Length; i += 2)
            {
                var indicator = args.Emote.Name.Substring(i, 2);
                var letter = convertRegionalToLetter(indicator);
                if(letter == null)
                { // not a regional indicator, so not a flag emote
                    return; // so probably not trying to translate
                }
                asLetters.Append(letter);
            }
            await args.Message.RemoveReactionAsync(args.Emote, args.User);
            var toLanguage = getLanguage(asLetters.ToString());
            if(toLanguage == null)
            {
                await args.User.SendMessageAsync($"You reacted with country flag `{args.Emote.Name}`, but I don't yet know what language that means. Please contact my owner.");
                return;
            }
            await Program.GlobalServices.GetRequiredService<TranslationService>()
                .Translate(args.Message, args.User, toLanguage);
        }

        public async Task Translate(IUserMessage arg, IUser manualRequester, string language)
        {
            if (!(Watched.TryGetValue(arg.Author.Id, out var users)))
            {
                if (manualRequester == null)
                    return; // manual request doesn't need to send to all users, so we can continue
            }
            if (users != null && users.Count == 0)
            {
                Watched.Remove(arg.Author.Id);
                if (manualRequester == null)
                    return;
            }
            var client = TranslationClient.Create();
            var response = await client.TranslateTextAsync(arg.Content, language);
            if(response.DetectedSourceLanguage == language)
            {
                if (manualRequester != null)
                    await manualRequester.SendMessageAsync($"Message has same detected language as wanting to translate into.");
            } else
            {
                var embedB = new EmbedBuilder();
                embedB.Title = $"Translation from {LanguageCodesUtils.ToName(response.DetectedSourceLanguage)}";
                embedB.Url = arg.GetJumpUrl();
                embedB.WithFooter(arg.Author.Username, arg.Author.GetAnyAvatarUrl());
                embedB.Description = response.TranslatedText;
                if (response.OriginalText.Length > 1024)
                {
                    embedB.AddField($"Original, part 1", Program.Clamp(response.OriginalText, 1024));
                    // 0 -> 1023
                    // 1024 -> 
                    embedB.AddField($"Original, part 2", response.OriginalText.Substring(1024));
                }
                else
                {
                    embedB.AddField("Original", Program.Clamp(response.OriginalText, 1024));
                }
                var embed = embedB.Build();
                if(manualRequester != null)
                {
                    await manualRequester.SendMessageAsync(embed: embed);
                    return;
                }
                var rm = new List<ulong>();
                foreach (var id in users)
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
                if (rm.Count > 0)
                {
                    Watched[arg.Author.Id] = users;
                    this.OnSave();
                }
            }
        }

        private async Task Client_MessageReceived(Discord.WebSocket.SocketMessage arg)
        {
            if (arg.Author.IsBot || arg.Author.IsWebhook || !(arg is IUserMessage userMessage))
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
            if(arg.Content.Length <= 8 || arg.Content.Split(' ').Length <= 1)
            {
                return;
            }
            if(arg.Content.StartsWith("!"))
            {
                // check if Rhythm in channel
                var a = await arg.Channel.GetUserAsync(235088799074484224); 
                if (a != null)
                    return;
            }
        }

        public JToken GetSARDataFor(ulong userId)
        {
            var dict = new Dictionary<string, List<string>>();
            foreach(var keypair in Watched)
            {
                if(keypair.Key == userId)
                {
                    dict[$"{userId}"] = keypair.Value.Select(x => "*other user data*").ToList();
                } else if(keypair.Value.Contains(userId))
                {
                    dict[$"*other user data*"] = new List<string>() { $"{userId}" };
                }
            }
            return JObject.FromObject(dict);
        }
    }
}
