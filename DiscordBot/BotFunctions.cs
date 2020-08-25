using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    public partial class Program
    {
        public static BotUser GetUserOrDefault(ulong id)
        {
            return Users.FirstOrDefault(x => x.Id == id);
        }
        public static BotUser GetUser(IUser user)
        {
            var existing = GetUserOrDefault(user.Id);
            if(existing == null)
            {
                existing = new BotUser(user);
                Program.Users.Add(existing);
            }
            return existing;
        }


        static SocketGuild m_logging;
        public static SocketGuild LoggingGuild
        {
            get
            {
                return m_logging ??= Client.GetGuild(ulong.Parse(Configuration["guilds:logging"]));
            }
        }
        static SocketGuild m_chess;
        public static SocketGuild ChessGuild => m_chess ??= Client.GetGuild(ulong.Parse(Configuration["guilds:chess"]));

        public static async Task<bool> IsPasswordLeaked(string password)
        {
            var client = Program.Services.GetRequiredService<HttpClient>();
            var hash = Hash.GetSHA1(password);
            var prefix = hash.Substring(0, 5);
            var suffix = hash.Substring(5);
            var response = await client.GetAsync($"https://api.pwnedpasswords.com/range/{prefix}");
            var content = await response.Content.ReadAsStringAsync();
            var lines = content.Split('\n');
            foreach(var line in lines)
            {
                var split = line.Split(':');
                if(int.Parse(split[1]) > 0)
                {
                    if (split[0] == suffix)
                        return true;
                }
            }
            return false;
        }

        public static OverwritePermissions ReadPerms
        {
            get
            {
                return new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Deny);
            }
        }
        public static OverwritePermissions WritePerms
        {
            get
            {
                return new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, readMessageHistory: PermValue.Allow);
            }
        }
        public static OverwritePermissions NoPerms
        {
            get
            {
                return new OverwritePermissions(viewChannel: PermValue.Deny, sendMessages: PermValue.Deny);
            }
        }
        public static OverwritePermissions FullPerms
        {
            get
            {
                return new OverwritePermissions(PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow, PermValue.Allow);
            }
        }

        static JsonSerializerSettings getSettings(params JsonConverter[] conv)
        {
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new DiscordConverter());
            settings.Converters.Add(new Classes.ServerList.MLJsonConverter());
            settings.Converters.Add(new Classes.ServerList.IPConverter());
            if(conv != null)
            {
                foreach (var x in conv)
                    settings.Converters.Add(x);
            }
            return settings;
        }

        public static string Serialise(object obj, params JsonConverter[] conv)
        {
            return JsonConvert.SerializeObject(obj, getSettings(conv));
        }
        public static T Deserialise<T>(string input, params JsonConverter[] conv)
        {
            return JsonConvert.DeserializeObject<T>(input, getSettings(conv));
        }

        public static Discord.Commands.TypeReaderResult AttemptParseInput<TArg>(string input) =>
            AttemptParseInput(input, typeof(TArg));
        public static Discord.Commands.TypeReaderResult AttemptParseInput(string input, Type desired)
        {
            var type = typeof(Discord.Commands.CommandService);
            var thing = type.GetField("_defaultTypeReaders", BindingFlags.NonPublic | BindingFlags.Instance);
            var defaultTypeReaders = thing.GetValue(Program.Commands) as IDictionary<Type, Discord.Commands.TypeReader>;
            var thing2 = type.GetField("_typeReaders", BindingFlags.NonPublic | BindingFlags.Instance);
            var ownTypeReaders = thing2.GetValue(Program.Commands) as System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Collections.Concurrent.ConcurrentDictionary<System.Type, Discord.Commands.TypeReader>>;

            Dictionary<Type, Discord.Commands.TypeReader> combined = new Dictionary<Type, Discord.Commands.TypeReader>();
            foreach (var keypair in defaultTypeReaders)
                combined.Add(keypair.Key, keypair.Value);
            foreach (var keypair in ownTypeReaders)
                combined[keypair.Key] = keypair.Value.Values.First();

            if(desired.BaseType == typeof(Array))
            { // we'll try to split the input, then parse each individual item.
                var typeOfElement = desired.GetMethod("Get").ReturnType;
                var elements = input.Split(',');
                dynamic array = Activator.CreateInstance(desired, elements.Length);
                for(int i = 0; i < elements.Length; i++)
                {
                    var inner = AttemptParseInput(elements[i], typeOfElement);
                    if (!inner.IsSuccess)
                        return TypeReaderResult.FromError(CommandError.ParseFailed, $"Attempts to parse element of {typeOfElement.Name}[] failed: {inner.ErrorReason}");
                    array[i] = (dynamic)inner.BestMatch;
                }
                return TypeReaderResult.FromSuccess(array);
            }

            var reader = combined[desired];
            if (reader == null)
            {
                return Discord.Commands.TypeReaderResult.FromError(
                    Discord.Commands.CommandError.Exception, $"Parser for {desired.Name} unavailabe");
            }
            var result = reader.ReadAsync(null, input, Program.Services).Result;
            if (result.IsSuccess)
            {
                return Discord.Commands.TypeReaderResult.FromSuccess(result.BestMatch);
            }
            else
            {
                return Discord.Commands.TypeReaderResult.FromError(
                    Discord.Commands.CommandError.ParseFailed, result.ErrorReason);
            }
        }

        public static string GetDaySuffix(int day)
        {
            switch (day)
            {
                case 1:
                case 21:
                case 31:
                    return "st";
                case 2:
                case 22:
                    return "nd";
                case 3:
                case 23:
                    return "rd";
                default:
                    return "th";
            }
        }
    
        public static string ToBase64(string utf)
        {
            var bytes = Encoding.UTF8.GetBytes(utf);
            return Convert.ToBase64String(bytes);
        }
        public static string ToUTF8(string b64)
        {
            var bytes = Convert.FromBase64String(b64);
            return Encoding.UTF8.GetString(bytes);
        }
    
        public static string Limit(string text, int maxLength, string ender = "...")
        {
            maxLength = maxLength - ender.Length;
            if(text.Length > maxLength)
            {
                text = text.Substring(0, maxLength) + ender;
            }
            return text;
        }

        public static void downloadFile(Uri uri, string path, WebClient wc = null)
        {
            if(wc == null)
            {
                wc = new WebClient();
                wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
                wc.DownloadFileCompleted += Wc_DownloadFileCompleted;
            }
            var fInfo = new FileInfo(path);
            if (!fInfo.Directory.Exists)
                fInfo.Directory.Create();
            wc.DownloadFileAsync(uri, path, uri.PathAndQuery);
        }

        private static void Wc_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if(e.Error != null)
            {
                LogMsg($"No download {e.UserState}: {e.Error}", LogSeverity.Error, "downloadFile");
            }
            else if (e.Cancelled)
            {
                LogMsg($"Cancelled download {e.UserState}", LogSeverity.Warning, "downloadFile");
            } else
            {
                LogMsg($"Finished downloading {e.UserState}", LogSeverity.Info, "downloadFile");
            }
        }
        private static void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.WriteLine($"{e.UserState}: {e.ProgressPercentage}%");
        }
    
        public static class RomanNumerals
        {
            public static readonly Dictionary<char, int> RomanNumberDictionary;
            public static readonly Dictionary<int, string> NumberRomanDictionary;

            static RomanNumerals()
            {
                RomanNumberDictionary = new Dictionary<char, int>
                {
                    { 'I', 1 },
                    { 'V', 5 },
                    { 'X', 10 },
                    { 'L', 50 },
                    { 'C', 100 },
                    { 'D', 500 },
                    { 'M', 1000 },
                };

                NumberRomanDictionary = new Dictionary<int, string>
                {
                    { 1000, "M" },
                    { 900, "CM" },
                    { 500, "D" },
                    { 400, "CD" },
                    { 100, "C" },
                    { 90, "XC" },
                    { 50, "L" },
                    { 40, "XL" },
                    { 10, "X" },
                    { 9, "IX" },
                    { 5, "V" },
                    { 4, "IV" },
                    { 1, "I" },
                };
            }

            public static string To(int number)
            {
                var roman = new StringBuilder();

                foreach (var item in NumberRomanDictionary)
                {
                    while (number >= item.Key)
                    {
                        roman.Append(item.Value);
                        number -= item.Key;
                    }
                }

                return roman.ToString();
            }

            public static int From(string roman)
            {
                int total = 0;

                int current, previous = 0;
                char currentRoman, previousRoman = '\0';

                for (int i = 0; i < roman.Length; i++)
                {
                    currentRoman = roman[i];

                    previous = previousRoman != '\0' ? RomanNumberDictionary[previousRoman] : '\0';
                    current = RomanNumberDictionary[currentRoman];

                    if (previous != 0 && current > previous)
                    {
                        total = total - (2 * previous) + current;
                    }
                    else
                    {
                        total += current;
                    }

                    previousRoman = currentRoman;
                }

                return total;
            }
        }
    }
}
