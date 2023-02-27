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
        public readonly bool _discord;
        public RequireAuthentication(bool requireAuth = true, bool requireDiscordConnection = true)
        {
            _auth = requireAuth;
            _discord = requireDiscordConnection;
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
                if(_discord)
                {
                    if (context.User.Connections.Discord == null)
                        return PreconditionResult.FromError("Requires a connected Discord account");
                }
            }
            return PreconditionResult.FromSuccess();
        }
    }
}
