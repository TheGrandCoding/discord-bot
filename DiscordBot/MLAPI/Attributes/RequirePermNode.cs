using System;
using System.Collections.Generic;
using System.Text;
using Discord.Commands;

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
            List<string> missing = new List<string>();
            foreach(var perm in Nodes)
            {
                if(!Perms.HasPerm(context.User, perm))
                {
                    missing.Add(perm);
                }
            }
            if(missing.Count > 0)
                return PreconditionResult.FromError("Missing permissions: '" + string.Join("', '", missing) + "'");
            return PreconditionResult.FromSuccess();
        }
    }
}
