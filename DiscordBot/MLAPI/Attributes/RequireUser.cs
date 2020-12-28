using System;
using System.Collections.Generic;
using System.Text;
using Discord.Commands;

namespace DiscordBot.MLAPI
{
    public class RequireUser : APIPrecondition
    {
        public readonly ulong _user;
        public RequireUser(ulong id)
        {
            _user = id;
        }

        public override bool CanChildOverride(APIPrecondition child)
        {
            return false;
        }

        public override PreconditionResult Check(APIContext context)
        {
            if(context.User != null)
            {
                if(context.User.Id == _user)
                {
                    return PreconditionResult.FromSuccess();
                }
            }
            return PreconditionResult.FromError("Authorised account does not have access to this endpoint");
        }
    }
}
