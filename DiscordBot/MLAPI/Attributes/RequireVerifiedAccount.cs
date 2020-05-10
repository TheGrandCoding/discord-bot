using Discord.Commands;
using DiscordBot.MLAPI.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class RequireVerifiedAccount : APIPrecondition
    {
        private readonly bool _require;
        public override bool CanChildOverride(APIPrecondition child)
        {
            return true;
        }
        public RequireVerifiedAccount(bool require = true)
        {
            _require = require;
        }

        public override PreconditionResult Check(APIContext context)
        {
            if (!_require)
                return PreconditionResult.FromSuccess();
            if (context.User == null)
                throw new RedirectException("/login", "Must authenticate");
            if(string.IsNullOrWhiteSpace(context.User.VerifiedEmail))
                throw new RedirectException(MLAPI.Modules.MicrosoftOauth.getUrl(context.User), "Must verify via MS Oauth");
            return PreconditionResult.FromSuccess();
        }
    }
}
