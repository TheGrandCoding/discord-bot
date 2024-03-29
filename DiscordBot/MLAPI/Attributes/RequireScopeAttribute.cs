﻿using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Permissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class RequireScopeAttribute : APIPrecondition
    {
        public readonly string Scope;
        public RequireScopeAttribute(string scope)
        {
            Scope = scope;
        }

        public override bool CanChildOverride(APIPrecondition child)
        {
            return true;
        }

        public override PreconditionResult Check(APIContext context, IServiceProvider services)
        {
            if (Scope == "*" || Scope == null)
                return PreconditionResult.FromSuccess();
            if (context.Session == null && context.Token == null)
                throw new MustLoginException();
            if (context.Token == null) // they're not using a token, so no scope to check
                return PreconditionResult.FromSuccess();
            var _scope = Scope;
            if(string.IsNullOrWhiteSpace(_scope))
            {
                var path = context.Path.Split("/", StringSplitOptions.RemoveEmptyEntries);
                _scope = "html." + string.Join(".", path);
            }
            foreach(var scope in context.Token.Scopes)
            {
                if (Perm.IsMatch(scope, _scope, out _))
                    return PreconditionResult.FromSuccess();
            }
            return PreconditionResult.FromError("Token is not authenticated for this endpoint");
        }
    }
}
