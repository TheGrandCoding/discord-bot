using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class RequireNoExcessQuery : APIPrecondition
    {
        public readonly bool Required;
        public RequireNoExcessQuery(bool required = true)
        {
            Required = required;
        }
        public override bool CanChildOverride(APIPrecondition child)
        {
            return true;
        }

        public override PreconditionResult Check(APIContext context, IServiceProvider services)
        {
            return PreconditionResult.FromSuccess();
        }
    }
}
