using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Permissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Commands
{
    public class BotCommandContext : SocketCommandContext
    {
        private BotDbContext _db;
        public BotDbContext BotDB { get
            {
                return _db ??= BotDbContext.Get(); // disposed via AfterExecute
            } 
        }
        public void Dispose()
        {
            _db?.SaveChanges();
            _db?.Dispose();
        }
        private BotDbUser _user;
        public BotDbUser BotDbUser 
        { 
            get
            {
                return _user ??= BotDB.GetUserFromDiscord(this.User, true).Result.Value;
            }
        }


        public bool HasPerm(NodeInfo node)
        {
            if (BotDbUser == null)
                return false;
            return PermChecker.UserHasPerm(BotDbUser, node);
        }

        public BotCommandContext(DiscordSocketClient client, SocketUserMessage msg) : base(client, msg)
        {
        }
    }
}
