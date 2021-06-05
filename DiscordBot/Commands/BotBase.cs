using Discord;
using Interactivity;
using Discord.Commands;
using DiscordBot.Services;
using DiscordBot.Services.BuiltIn;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace DiscordBot.Commands
{
    public abstract class BotBase : ModuleBase<BotCommandContext>
    {
        protected InteractivityService InteractivityService { get; set; }
        static CmdDisableService cmdDisableService { get; set; }
        protected override void BeforeExecute(CommandInfo command)
        {
            cmdDisableService ??= Program.Services.GetRequiredService<CmdDisableService>();
            if (cmdDisableService.IsDisabled(command, out string reason))
                throw new Exception($"{reason}");
        }

        public RuntimeResult Success(string message = null, bool isTTS = false, Embed embed = null)
        {
            if(message != null || embed != null)
                ReplyAsync("✅ " + message, isTTS, embed).Wait();
            return new BotResult();
        }
        public RuntimeResult Error(string message)
            => new BotResult(message);

        public Task<InteractivityResult<object>> PagedReplyAsync(Interactivity.Pagination.PaginatorBuilder builder, TimeSpan? timeout = null)
        {
            return InteractivityService.SendPaginatorAsync(builder.Build(),
                Context.Channel, timeout: timeout);
        }
        public Task<InteractivityResult<SocketMessage>> NextMessageAsync(TimeSpan? timeout = null)
        {
            return InteractivityService.NextMessageAsync(timeout: timeout);
        }
    }
}
