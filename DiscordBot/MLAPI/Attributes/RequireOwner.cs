using System;
using System.Collections.Generic;
using System.Text;
using Discord.Commands;

namespace DiscordBot.MLAPI
{
    public class RequireOwner : APIPrecondition
    {
        public override bool CanChildOverride(APIPrecondition child)
        {
            return false;
        }

        public override PreconditionResult Check(APIContext context, IServiceProvider services)
        {
            return (context.User?.Connections?.DiscordId ?? "") == (Program.AppInfo?.Owner?.Id ?? 1).ToString() 
                ? PreconditionResult.FromSuccess() 
                : PreconditionResult.FromError("Only the bot's owner may access this");
        }
    }
}
