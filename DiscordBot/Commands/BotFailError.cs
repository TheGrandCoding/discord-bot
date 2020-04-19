using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Commands
{
    public class BotResult : RuntimeResult
    {
        public BotResult() : base(null, null)
        {
        }
        public BotResult(string m) : base(CommandError.Unsuccessful, m)
        {
        }
    }
}
