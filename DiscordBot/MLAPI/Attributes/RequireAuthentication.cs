using Discord.Commands;
using DiscordBot.MLAPI.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class RequireAuthentication : APIPrecondition
    {
        private readonly bool _auth;
        public RequireAuthentication(bool requireAuth = true)
        {
            _auth = requireAuth;
        }

        public override bool CanChildOverride(APIPrecondition child)
        {
            return true;
        }

        public override PreconditionResult Check(APIContext context)
        {
            return (context.User != null && _auth) || (!_auth)
                ? PreconditionResult.FromSuccess()
                : throw new RedirectException("/login", $"Requires authentication");
        }
    }
}
