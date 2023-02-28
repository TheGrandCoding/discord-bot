using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public static string GetStackTrace(string sep = "\r\n- ")
        {
            var stack = new StackTrace(1, true);
            var ss = new StringBuilder();
            ss.Append($"DB, stack:");
            foreach (var frame in stack.GetFrames())
            {
                if (frame.GetFileLineNumber() == 0)
                    continue;
                var fName = Path.GetFileName(frame.GetFileName());
                var caller = "";
                var method = frame.GetMethod();
                if (method != null)
                {
                    caller = $"{method.DeclaringType?.Name}.{method.Name}";
                }
                else
                {
                    caller = $"<unknown>";
                }
                ss.Append($"\r\n- {caller} #{frame.GetFileLineNumber()}");
            }
            return ss.ToString();
        }

        public static string Clamp(string str, int length)
        {
            if (str.Length <= length)
                return str;
            return str.Substring(0, length);
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

        public static async Task<bool> IsPasswordLeaked(BotHttpClient client, string password)
        {
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

        static JsonSerializerSettings getSettings(TypeNameHandling handling, Formatting format, params JsonConverter[] conv)
        {
            var settings = new JsonSerializerSettings();
            settings.Formatting = format;
            settings.TypeNameHandling = handling;
            settings.Converters.Add(new DiscordConverter());
            //settings.Converters.Add(new Classes.ServerList.MLJsonConverter());
            //settings.Converters.Add(new Classes.ServerList.IPConverter());
            if(conv != null)
            {
                foreach (var x in conv)
                    settings.Converters.Add(x);
            }
            return settings;
        }

        public static string Serialise(object obj, TypeNameHandling handling = TypeNameHandling.None, Formatting format = Formatting.None, params JsonConverter[] conv)
        {
            return JsonConvert.SerializeObject(obj, getSettings(handling, format, conv));
        }
        public static T Deserialise<T>(string input, params JsonConverter[] conv)
        {
            return JsonConvert.DeserializeObject<T>(input, getSettings(TypeNameHandling.Auto, Formatting.None, conv));
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
            var nullableType = Nullable.GetUnderlyingType(desired);
            if(nullableType != null)
            {
                // it's a nullable type, so we'll try to parse if the text isn't null or empty.
                if (string.IsNullOrWhiteSpace(input))
                {
                    return TypeReaderResult.FromSuccess(null);
                }
                else
                {
                    Program.LogInfo($"Recursing to parse {input} to {desired.FullName}; now nullable {nullableType.FullName}", "Program.AttemptParse");
                    return AttemptParseInput(input, nullableType);
                }
            }

            var reader = combined[desired];
            if (reader == null)
            {
                return Discord.Commands.TypeReaderResult.FromError(
                    Discord.Commands.CommandError.Exception, $"Parser for {desired.Name} unavailabe");
            }
            var result = reader.ReadAsync(null, input, Program.GlobalServices).Result;
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


        static List<char> _braille;
        public static string ToBase64(string utf)
        {
            var bytes = Encoding.UTF8.GetBytes(utf);
            return Convert.ToBase64String(bytes);
        }
        public static string FromBase64(string b64)
        {
            var bytes = Convert.FromBase64String(b64);
            return Encoding.UTF8.GetString(bytes);
        }
        public static string ToHex(string utf)
        {
            var bytes = Encoding.UTF8.GetBytes(ToBase64(utf));
            return string.Join("", bytes.Select(x => x.ToString("X")));
        }
        public static string FromHex(string hex)
        {
            var bytes = new List<byte>();
            for(int i = 0; i < hex.Length - 1; i += 2)
            {
                var hexChar = hex.Substring(i, 2);
                var b = byte.Parse(hexChar, System.Globalization.NumberStyles.HexNumber);
                bytes.Add(b);
            }
            var b64 = Encoding.UTF8.GetString(bytes.ToArray());
            return FromBase64(b64);
        }
        public static string ToEncoded(string utf8)
            => substitute(ToHex(utf8), true);
        public static string FromEncoded(string encoded)
        {
            var hex = substitute(encoded, false);
            return FromHex(hex);
        }

        static string substitute(string message, bool convert = true)
        {
            var result = new StringBuilder();
            var index = 0;
            if(convert)
            {
                for (int i = 0; i < message.Length - 1; i += 2)
                {
                    result.Append(_braille[int.Parse(message.Substring(i, 2), System.Globalization.NumberStyles.HexNumber)]);
                }
            } else
            {
                foreach(var chr in message)
                {
                    index = _braille.IndexOf(chr);
                    result.Append($"0{index:X}"[^2..]);
                }
            }
            return result.ToString();
        }


        static void appendN(StringBuilder b, int n, string sing, string plural = null)
        {
            if(plural == null)
            {
                if (sing.Length == 1)
                    plural = sing;
                else
                    plural = (plural ?? sing) + "s";
            }
            var spc = sing == plural ? "" : " ";
            if (n > 0)
                b.Append($"{n}{spc}{(n > 1 ? plural : sing)}{spc}");
        }
        public static string FormatTimeSpan(TimeSpan ts, bool shortForm = false, bool includeMs = false)
        {
            var builder = new StringBuilder();
            int days = ts.Days;
            int years = days / 365;
            days -= years * 365;
            appendN(builder, years, shortForm ? "y" : "year");
            appendN(builder, days, shortForm ? "d" : "day");
            appendN(builder, ts.Hours, shortForm ? "h" : "hour");
            appendN(builder, ts.Minutes, shortForm ? "m" : "minute");
            appendN(builder, ts.Seconds, shortForm ? "s" : "second");
            if(includeMs)
                appendN(builder, ts.Milliseconds, shortForm ? "ms" : "milisecond", shortForm ? "ms" : null);
            return builder.ToString();
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
                LogError($"No download {e.UserState}: {e.Error}", "downloadFile");
            }
            else if (e.Cancelled)
            {
                LogWarning($"Cancelled download {e.UserState}", "downloadFile");
            } else
            {
                LogInfo($"Finished downloading {e.UserState}", "downloadFile");
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
    
        public static bool IsNullable(Type type, out Type underlying)
        {
            underlying = null;
            var isNullable = type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(Nullable<>);
            if(isNullable)
                underlying = Nullable.GetUnderlyingType(type);
            return isNullable;
        }
        public static string GetTypeName(Type type, bool specifyEnumName = false)
        {
            if (type == typeof(int))
                return "int";
            if (type == typeof(bool))
                return "bool";
            if (!specifyEnumName && type.IsEnum)
                return "enum";
            if (type.FullName.StartsWith("System"))
                return type.FullName["System.".Length..].ToLower();
            return type.FullName;
        }
        public static string GetTypeName(Type type, out bool isNullable, bool specifyEnum = false)
        {
            isNullable = IsNullable(type, out var nl);
            return GetTypeName(nl ?? type, specifyEnum);
        }
    
        
        public static async Task LogDiscord(IMessageChannel channel, string message)
        {
            if(message.Length < 2000)
            {
                await channel.SendMessageAsync(message, allowedMentions: AllowedMentions.None);
            } else if(message.Length < 4000)
            {
                await channel.SendMessageAsync(embed: new EmbedBuilder().WithDescription(message).Build());
            } else
            {
                var path = Path.Combine(Path.GetTempPath(), "log.txt");
                await File.WriteAllTextAsync(path, message);
                await channel.SendFileAsync(path);
            }
        }
        public static async Task LogOwner(string message)
        {
            await LogDiscord(await Program.AppInfo.Owner.CreateDMChannelAsync(), message);
        }
        public static Task LogOwner(StringBuilder builder)
            => LogOwner(builder.Length > 4000 ? builder.ToString() : $"```\r\n{builder}\r\n```");
    
        public static string GetIP(string forwardedHeader, IPAddress addr)
        {
            if (string.IsNullOrWhiteSpace(forwardedHeader)) return addr.ToString();
            var comma = forwardedHeader.IndexOf(',');
            if (comma > -1)
                return forwardedHeader.Substring(0, comma);
            return forwardedHeader;
        }
    
    
        public static string GetTempPath(string filename)
        {
            return System.IO.Path.Combine(Path.GetTempPath(), filename);
        }

        public static string GetSafePath(string pathstr)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                pathstr = pathstr.Replace(c, '-');
            }
            return pathstr;
        }
    }
}
