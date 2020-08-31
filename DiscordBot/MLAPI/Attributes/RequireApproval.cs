using Discord.Commands;
using DiscordBot.MLAPI.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class RequireApprovalAttribute : APIPrecondition
    {
        private readonly bool _require;
        public RequireApprovalAttribute(bool require = true)
        {
            _require = require;
        }

        public override bool CanChildOverride(APIPrecondition child)
        {
            return true;
        }

        public override PreconditionResult Check(APIContext context)
        {
            if (!_require)
                return PreconditionResult.FromSuccess();
            if (context.User == null)
                throw new RedirectException("/login", "Requires login");
            if (context.User.IsApproved != true)
                throw new RedirectException("/login/approval", "User requires manual approval");
            return PreconditionResult.FromSuccess();
        }
    }
}
