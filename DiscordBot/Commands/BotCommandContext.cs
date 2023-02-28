using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Permissions;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Commands
{
    public class BotCommandContext : SocketCommandContext
    {
        private BotDbContext _db;
        private IServiceScope _scope = Program.GlobalServices.CreateScope();
        public BotDbContext BotDB { get
            {
                return _db ??= _scope.ServiceProvider.GetBotDb($"BotCmdCtx:{Message.Id}"); // disposed via AfterExecute
            } 
        }
        public void Dispose()
        {
            _db?.SaveChanges();
            _scope?.Dispose();
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
