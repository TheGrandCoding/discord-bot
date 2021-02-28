using Discord.Commands;
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

        public override PreconditionResult Check(APIContext context)
        {
            if (Scope == "*" || Scope == null)
                return PreconditionResult.FromSuccess();
            if (context.Token == null)
                throw new MustLoginException();
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
