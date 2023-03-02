using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions
{
    public abstract class BotSlashBase : Discord.Interactions.InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
    {
        private IServiceScope _scope = Program.GlobalServices.CreateScope();
        public IServiceProvider Services => _scope.ServiceProvider;
        private BotDbContext _db;
        public BotDbContext BotDB { get
            {
                return _db ??= Services.GetBotDb($"BotSlashBase"); // scoped
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
            await base.AfterExecuteAsync(command);
            if(_db != null)
                await _db.SaveChangesAsync();
            _scope?.Dispose();
        }
    }

    public abstract class BotComponentBase : Discord.Interactions.InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
    {
        private IServiceScope _scope = Program.GlobalServices.CreateScope();
        public IServiceProvider Services { get; set; }
        private BotDbContext _db;
        public BotDbContext BotDB
        {
            get
            {
                return _db ??= Services.GetBotDb("BotComponentBase"); // ds via AfterExecute
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
            await base.AfterExecuteAsync(command);
            await _db?.SaveChangesAsync();
            _scope?.Dispose();
        }
    }
}
