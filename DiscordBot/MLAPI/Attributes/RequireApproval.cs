﻿using Discord.Commands;
using DiscordBot.MLAPI.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class RequireApprovalAttribute : APIPrecondition
    {
        public readonly bool _require;
        public RequireApprovalAttribute(bool require = true)
        {
            _require = require;
        }

        public override bool CanChildOverride(APIPrecondition child)
        {
            return true;
        }

        public override PreconditionResult Check(APIContext context, IServiceProvider services)
        {
            if (!_require)
                return PreconditionResult.FromSuccess();
            if (context.User == null)
                throw new MustLoginException();
            if (context.User.Approved != true)
                throw new RedirectException("/login/approval", "User requires manual approval");
            return PreconditionResult.FromSuccess();
        }
    }
}
