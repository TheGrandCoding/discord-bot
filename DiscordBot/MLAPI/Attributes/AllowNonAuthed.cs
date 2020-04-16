using System;
using System.Collections.Generic;
using System.Text;
using Discord.Commands;

namespace DiscordBot.MLAPI
{
    public class AllowNonAuthed : APIPrecondition
    {
        public bool ConditionIfAuthed { get; set; } = false;
        public override bool CanChildOverride(APIPrecondition child)
        {
            return false;
        }

        public override PreconditionResult Check(APIContext context)
        {
            if (context.User == null)
                return PreconditionResult.FromSuccess();
            return ConditionIfAuthed ? PreconditionResult.FromSuccess() : PreconditionResult.FromError("Only non-authed users may view this");
        }
    }
}
