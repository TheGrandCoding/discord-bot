using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Permissions
{
    public class PermChecker
    {
        public List<Perm> Allows { get; set; } = new List<Perm>();
        public List<Perm> Denies { get; set; } = new List<Perm>();
        public List<Perm> Grants { get; set; } = new List<Perm>();

        public NodeInfo Seeking { get; set; }

        public PermChecker(NodeInfo checkThis, List<Perm> perms)
        {
            Seeking = checkThis;
            foreach(var p in perms)
            {
                if (p.Type == PermType.Allow)
                    Allows.Add(p);
                else if (p.Type == PermType.Deny)
                    Denies.Add(p);
                else
                    Grants.Add(p);
            }
        }

        public bool Check(out bool inheritsPerm)
        {
            foreach (var x in Allows)
                if (x.isMatch(Seeking, out inheritsPerm))
                    return true;
            foreach (var x in Denies)
                if (x.isMatch(Seeking, out inheritsPerm))
                    return false;
            foreach (var x in Grants)
                if (x.isMatch(Seeking, out inheritsPerm))
                    return true;
            inheritsPerm = false;
            return false;
        }

        public static bool UserHasPerm(BotUser user, NodeInfo seeking, out bool inheritsPerm)
        {
            inheritsPerm = false;
            if (user == null)
                return false;
            return new PermChecker(seeking, user.Permissions).Check(out inheritsPerm);
        }
        public static bool UserHasPerm(BotUser user, NodeInfo seeking) => UserHasPerm(user, seeking, out _);
        public static bool HasPerm(Commands.BotCommandContext context, NodeInfo seeking) => UserHasPerm(context.BotUser, seeking);
        public static bool HasPerm(MLAPI.APIContext context, NodeInfo seeking) => UserHasPerm(context.User, seeking);
    }
}
