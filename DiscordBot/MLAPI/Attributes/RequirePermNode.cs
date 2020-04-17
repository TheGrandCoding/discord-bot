using System;
using System.Collections.Generic;
using System.Text;
using Discord.Commands;
using DiscordBot.Classes;

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
            List<Perm> missing = new List<Perm>();
            foreach(var perm in Nodes)
            {
                var p = Perms.Parse(perm);
                var val = p?.HasPerm(context) ?? false;
                if(!val)
                {
                    missing.Add(p);
                }
            }
            if(missing.Count > 0)
                return PreconditionResult.FromError("Missing permissions: '" + string.Join("', '", missing) + "'");
            return PreconditionResult.FromSuccess();
        }
    }
}
