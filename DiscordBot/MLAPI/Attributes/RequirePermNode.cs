using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Permissions;

namespace DiscordBot.MLAPI
{
    public class RequirePermNode : APIPrecondition
    {
        public string[] Nodes;
        public RequirePermNode(params string[] permissions)
        {
            Nodes = permissions;
        }
        public override bool CanChildOverride(APIPrecondition child)
        {
            return true;
        }

        public override PreconditionResult Check(APIContext context)
        {
            if (context.User == null)
                return PreconditionResult.FromError("You must be logged in");
            List<NodeInfo> missing = new List<NodeInfo>();
            foreach(var perm in Nodes)
            {
                var node = (NodeInfo)perm;
                var val = PermChecker.HasPerm(context, node);
                if(!val)
                {
                    missing.Add(node);
                }
            }
            if(missing.Count > 0)
                return PreconditionResult.FromError("Missing permissions: '" + string.Join("', '", missing.Select(x => x.Description)) + "'");
            return PreconditionResult.FromSuccess();
        }
    }
}
