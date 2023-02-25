using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions
{
    public abstract class BotSlashBase : Discord.Interactions.InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
    {
        private BotDbContext _db;
        public BotDbContext BotDB { get
            {
                return _db ??= BotDbContext.Get();
            } }
        private BotDbUser _user;
        public BotDbUser User
        {
            get
            {
                return _user ??= BotDB.GetUserFromDiscord(Context.Interaction.User, true).Result.Value;
            }
        }

        public override async Task AfterExecuteAsync(ICommandInfo command)
        {
            await _db?.SaveChangesAsync();
            await base.AfterExecuteAsync(command);
        }
    }

    public abstract class BotComponentBase : Discord.Interactions.InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
    {
        private BotDbContext _db;
        public BotDbContext BotDB
        {
            get
            {
                return _db ??= BotDbContext.Get();
            }
        }
        private BotDbUser _user;
        public BotDbUser User
        {
            get
            {
                return _user ??= BotDB.GetUserFromDiscord(Context.Interaction.User, true).Result.Value;
            }
        }

        public override async Task AfterExecuteAsync(ICommandInfo command)
        {
            await _db?.SaveChangesAsync();
            await base.AfterExecuteAsync(command);
        }
    }
}
