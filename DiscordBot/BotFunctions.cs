using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
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
                new Classes.ServerList.MLJsonConverter());
        }
        public static T Deserialise<T>(string input)
        {
            return JsonConvert.DeserializeObject<T>(input, 
                new DiscordConverter(), 
                new Classes.ServerList.MLJsonConverter());
        }
    }
}
