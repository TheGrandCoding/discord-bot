using Discord.Commands;
using DiscordBot.MLAPI.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class RequireAuthentication : APIPrecondition
    {
        public readonly bool _auth;
        public readonly bool _valid;
        public RequireAuthentication(bool requireAuth = true, bool requireValid = true)
        {
            _auth = requireAuth;
            _valid = requireValid;
        }

        public override bool CanChildOverride(APIPrecondition child)
        {
            return true;
        }

        public override PreconditionResult Check(APIContext context)
        {
            if(_auth)
            {
                if (context.User == null)
                    throw new RedirectException("/login", "Requires authentication");
                if(_valid)
                {
                    if (context.User.DiscriminatorValue == 0 || string.IsNullOrWhiteSpace(context.User.Name))
                        return PreconditionResult.FromError("Requires valid user account");
                }
            }
            return PreconditionResult.FromSuccess();
        }
    }
}
