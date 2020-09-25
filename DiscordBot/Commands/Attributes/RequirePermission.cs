using Discord.Commands;
using DiscordBot.Permissions;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    public class RequirePermission : PreconditionAttribute
    {
        public readonly string Node;

        public RequirePermission(string node)
        {
            Node = node;
        }

        public async override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var node = Program.Services.GetRequiredService<PermissionsService>().FindNode(Node);
            if (!(context is BotCommandContext bC))
                return PreconditionResult.FromError("Command context invalid, contact bot developer.");
            if (PermChecker.HasPerm(bC, node))
                return PreconditionResult.FromSuccess();
            return PreconditionResult.FromError($"You required extra permissions to access this endpoint (`{node.Node}`: {node.Description})");
        }
    }
}
