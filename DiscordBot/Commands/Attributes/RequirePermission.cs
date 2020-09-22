using Discord.Commands;
using DiscordBot.Permissions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    public class RequirePermission : PreconditionAttribute
    {
        public readonly NodeInfo Node;

        public RequirePermission(string node)
        {
            Node = node;
        }

        public async override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (!(context is BotCommandContext bC))
                return PreconditionResult.FromError("Command context invalid, contact bot developer.");
            if (PermChecker.HasPerm(bC, Node))
                return PreconditionResult.FromSuccess();
            return PreconditionResult.FromError($"You required extra permissions to access this endpoint (`{Node.Node}`: {Node.Description})");
        }
    }
}
