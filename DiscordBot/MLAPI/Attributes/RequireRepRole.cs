using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Permissions;

namespace DiscordBot.MLAPI
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RequireRepublishRole : APIPrecondition
    {
        private BotRepublishRoles _role;
        public RequireRepublishRole(BotRepublishRoles role)
        {
            _role = role;
        }
        public override bool CanChildOverride(APIPrecondition child)
        {
            return true;
        }

        public override PreconditionResult Check(APIContext context, IServiceProvider services)
        {
            if (context.User == null)
                return PreconditionResult.FromError("You must be logged in");
            if(!context.User.RepublishRole.HasFlag(_role))
                return PreconditionResult.FromError($"Missing permissions: '{_role}'");
            return PreconditionResult.FromSuccess();
        }
    }
}
