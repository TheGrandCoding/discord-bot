using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Permissions;
using DiscordBot.TypeReaders;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("perms")]
    [Name("Bot Permissions Module")]
    public class PermsModule : BotModule
    {
        [Command("list")]
        [Summary("Lists all nodes, with an optional prefix search")]
        public async Task ListAllPermissions(string prefix = null)
        {
            var pages = new List<string>();
            string cPage = "";
            foreach(var node in Perms.AllNodes.Values)
            {
                if (prefix != null && !node.Node.StartsWith(prefix))
                    continue;
                var s = $"`{node.Node}` {node.Description}\r\n";
                if((cPage.Length + s.Length) > 1024)
                {
                    pages.Add(cPage);
                    cPage = "";
                }
                cPage += s;
            }
            if (cPage == "")
                cPage = "No permissions nodes are known.";
            pages.Add(cPage);
            await PagedReplyAsync(pages);
        }

        async Task printListFor(BotUser u)
        {
            var paginator = new PaginatedMessage();
            paginator.Title = $"Permissions of {u.Name}";
            var pages = new List<string>();
            string cPage = "";
            foreach (var perm in u.Permissions)
            {
                string sep = perm.Type == PermType.Allow ? "**" : perm.Type == PermType.Deny ? "~~" : "";
                var s = $"`{perm.RawNode}` {sep}{perm.Description}{sep}\r\n";
                if ((cPage.Length + s.Length) > 1024)
                {
                    pages.Add(cPage);
                    cPage = "";
                }
                cPage += s;
            }
            if (cPage == "")
                cPage = "No permissions nodes are known.";
            pages.Add(cPage);
            paginator.Pages = pages;
            await PagedReplyAsync(paginator);
        }

        async Task printActualFor(BotUser u)
        {
            var paginator = new PaginatedMessage();
            paginator.Title = $"Permissions of {u.Name}";
            paginator.Content = "These are the effective permissions of the user, taking into account wildcards, denials and allowals.";
            var pages = new List<string>();
            string cPage = "";
            foreach (var perm in Perms.AllNodes.Values)
            {
                if (!PermChecker.UserHasPerm(u, perm, out var inherit))
                    continue;
                string sep = inherit ? "**" : "";
                var s = $"`{perm.Node}` {sep}{perm.Description}{sep}\r\n";
                if ((cPage.Length + s.Length) > 1024)
                {
                    pages.Add(cPage);
                    cPage = "";
                }
                cPage += s;
            }
            if (cPage == "")
                cPage = "None.";
            pages.Add(cPage);
            paginator.Pages = pages;
            await PagedReplyAsync(paginator);
        }

        async Task doCheckFor(BotUser u, NodeInfo node)
        {
            var given = PermChecker.UserHasPerm(Context.BotUser, node, out var b);
            if (given)
            {
                await ReplyAsync($":ballot_box_with_check: {(u.Id == Context.User.Id ? "You have" : $"{u.Name} has")} that node {(b ? "via a wildcard" : "directly")}");
            }
            else
            {
                await ReplyAsync($":x: {(u.Id == Context.User.Id ? "You do not have" : $"{u.Name} does not have")} that permission.");
            }
        }
    
        [Command("view")]
        [Summary("View your own permissions")]
        public async Task ViewOwn()
        {
            await printListFor(Context.BotUser);
        }

        [Command("view")]
        [Alias("viewo")]
        [Summary("View someone else's permissions")]
        [RequirePermission(Perms.Bot.Developer.ViewPermissions)]
        public async Task ViewOther(BotUser u) => await printListFor(u);


        [Command("effective")]
        [Summary("View your effective permissions")]
        public async Task ViewEffectiveOwn()
        {
            await printActualFor(Context.BotUser);
        }

        [Command("effective")]
        [Alias("effectiveo")]
        [Summary("View someone else's effective permissions")]
        [RequirePermission(Perms.Bot.Developer.ViewPermissions)]
        public async Task ViewEffectiveOther(BotUser u) => await printActualFor(u);

        [Command("check"), Alias("test")]
        [Summary("Checks whether you have the provided node")]
        public async Task Test(string nodeText)
        {
            await doCheckFor(Context.BotUser, nodeText);
        }

        [Command("check"), Alias("test", "checko", "testo")]
        [Summary("Checks whether another user has the provided node")]
        [RequirePermission(Perms.Bot.Developer.ViewPermissions)]
        public async Task Test(BotUser user, string nodeText)
        {
            await doCheckFor(user, nodeText);
        }
    
        [Command("grant")]
        [Summary("Gives or revokes the permission to or from the user")]
        [RequirePermission(Perms.Bot.All)]
        public async Task<RuntimeResult> GrantPerm(BotUser user, string nodeText)
        {
            var node = (NodeInfo)nodeText;
            if (!Context.HasPerm(node))
                return new BotResult("You must have the permission you wish to give.");
            if(PermChecker.UserHasPerm(user, node, out bool inherit))
            {
                if(!inherit)
                {
                    var i = user.Permissions.RemoveAll(x => x.Type == PermType.Grant && x.Node == node.Node);
                    await ReplyAsync($"Removed {i} perms that match the node.");
                    return new BotResult();
                }
            }
            user.Permissions.Add(new Perm(node, PermType.Grant));
            await printListFor(user);
            return new BotResult();
        }

        [Command("deny")]
        [Summary("Toggles denial of the permission")]
        [RequirePermission(Perms.Bot.All)]
        public async Task<RuntimeResult> DenyPerm(BotUser user, string nodeText)
        {
            var node = (NodeInfo)nodeText;
            if (!Context.HasPerm(node))
                return new BotResult("You must have the permission you wish to deny.");
            var i = user.Permissions.RemoveAll(x => x.Type == PermType.Deny && x.Node == node.Node);
            if(i > 0)
            {
                await ReplyAsync("Removed denial of permission.");
            } else
            {
                user.Permissions.Add(new Perm(node, PermType.Deny));
            }
            await printListFor(user);
            return new BotResult();
        }

        [Command("allow")]
        [Summary("Toggles allowal of the permission")]
        [RequirePermission(Perms.Bot.All)]
        public async Task<RuntimeResult> AllowPerm(BotUser user, string nodeText)
        {
            var node = (NodeInfo)nodeText;
            if (!Context.HasPerm(node))
                return new BotResult("You must have the permission you wish to allow.");
            var i = user.Permissions.RemoveAll(x => x.Type == PermType.Allow && x.Node == node.Node);
            if (i > 0)
            {
                await ReplyAsync("Removed allowal of permission.");
            }
            else
            {
                user.Permissions.Add(new Perm(node, PermType.Allow));
            }
            await printListFor(user);
            return new BotResult();
        }


    }
}
