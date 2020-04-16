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

        public override PreconditionResult Check(APIContext context)
        {
            return (context.User?.Id ?? 0) == 144462654201790464 
                ? PreconditionResult.FromSuccess() 
                : PreconditionResult.FromError("Only the bot's owner may access this");
        }
    }
}
