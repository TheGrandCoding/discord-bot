using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using DiscordBot.Services;
using DiscordBot.Services.BuiltIn;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    public class BotModule : InteractiveBase<BotCommandContext>
    {
        static CmdDisableService cmdDisableService { get; set; }
        protected override void BeforeExecute(CommandInfo command)
        {
            cmdDisableService ??= Program.Services.GetRequiredService<CmdDisableService>();
            if (cmdDisableService.IsDisabled(command, out string reason))
                throw new Exception($"{reason}");
        }

        public async Task<IUserMessage> Success(string message, bool isTTS = false, Embed embed = null)
            => await ReplyAsync("✅ " + message, isTTS, embed);
    }
}
