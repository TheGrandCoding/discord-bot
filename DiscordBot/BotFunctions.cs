using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

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


        public static string Serialise(object obj)
        {
            return JsonConvert.SerializeObject(obj, 
                new DiscordConverter(), 
                new Classes.ServerList.MLJsonConverter(),
                new Classes.ServerList.IPConverter());
        }
        public static T Deserialise<T>(string input)
        {
            return JsonConvert.DeserializeObject<T>(input, 
                new DiscordConverter(), 
                new Classes.ServerList.MLJsonConverter(),
                new Classes.ServerList.IPConverter());
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
    }
}
