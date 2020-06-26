using Discord.Commands;
using DiscordBot.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    [Group("bot")]
    [Name("Bot Commands")]
    public class BotCmdModule : BotModule
    {
        [Command("close")]
        [Summary("Closes the bot")]
        public async Task Close(int code = 0)
        {
            await Success("Closing");
            Program.Close(code);
        }
    }
}
