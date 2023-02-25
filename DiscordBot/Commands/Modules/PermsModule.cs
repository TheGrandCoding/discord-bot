using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Permissions;
using DiscordBot.Services;
using DiscordBot.TypeReaders;
using Interactivity;
using Interactivity.Pagination;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("perms")]
    [Name("Bot Permissions Module")]
    public class PermsModule : BotBase
    {
        public PermissionsService Service { get; set; }
        [Command("list")]
        [Summary("Lists all nodes, with an optional prefix search")]
        public async Task ListAllPermissions(string prefix = null)
        {
            var paginator = new StaticPaginatorBuilder();
            PageBuilder cPage = new PageBuilder();
            foreach(var node in Service.AllNodes.Values)
            {
                if (prefix != null && !node.Node.StartsWith(prefix))
                    continue;
                var s = $"`{node.Node}` {node.Description}\r\n";
                if((cPage.Text.Length + s.Length) > 1024)
                {
                    paginator.AddPage(cPage);
                    cPage = new PageBuilder();
                }
                cPage.Text += s;
            }
            if (cPage.Text == "")
                cPage.Text = "No permissions nodes are known.";
            paginator.AddPage(cPage);
            await PagedReplyAsync(paginator);
        }

        async Task printListFor(BotDbUser u)
        {
            var paginator = new StaticPaginatorBuilder();
            var cPage = new PageBuilder();
            foreach (var botperm in u.Permissions)
            {
                var perm = botperm.PermNode;
                string sep = perm.Type == PermType.Allow ? "**" : perm.Type == PermType.Deny ? "~~" : "";
                var s = $"`{perm.RawNode}` {sep}{perm.Description}{sep}\r\n";
                if ((cPage.Text.Length + s.Length) > 1024)
                {
                    paginator.AddPage(cPage);
                    cPage = new PageBuilder();
                }
                cPage.Text += s;
            }
            if (cPage.Text == "")
                cPage.Text = "No permissions nodes are known.";
            paginator.AddPage(cPage);
            await PagedReplyAsync(paginator);
        }

        async Task printActualFor(BotDbUser u)
        {
            var paginator = new StaticPaginatorBuilder();
            var cPage = new PageBuilder();
            cPage.Title = $"Permissions of {u.Name}";
            cPage.Text = "These are the effective permissions of the user, taking into account wildcards, denials and allowals.";
            foreach (var perm in Service.AllNodes.Values)
            {
                if (!PermChecker.UserHasPerm(u, perm, out var inherit))
                    continue;
                string sep = inherit ? "**" : "";
                var s = $"`{perm.Node}` {sep}{perm.Description}{sep}\r\n";
                if ((cPage.Description.Length + s.Length) > 1024)
                {
                    paginator.AddPage(cPage);
                    cPage = new PageBuilder();
                    cPage.Title = $"Permissions of {u.Name}";
                    cPage.Text = "These are the effective permissions of the user, taking into account wildcards, denials and allowals.";

                }
                cPage.Description += s;
            }
            if (cPage.Description == "")
                cPage.Description = "None.";
            paginator.AddPage(cPage);
            await PagedReplyAsync(paginator);
        }

        async Task doCheckFor(BotDbUser u, NodeInfo node)
        {
            var given = PermChecker.UserHasPerm(Context.BotDbUser, node, out var b);
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
            await printListFor(Context.BotDbUser);
        }

        [Command("view")]
        [Alias("viewo")]
        [Summary("View someone else's permissions")]
        [RequirePermission(Perms.Bot.Developer.ViewPermissions)]
        public async Task ViewOther(BotDbUser u) => await printListFor(u);

        [Command("whohas")]
        [Summary("Lists who has the given permission")]
        [RequirePermission(Perms.Bot.Developer.ViewPermissions)]
        public async Task<RuntimeResult> WhoHas(string node)
        {
            var perm = Service.FindNode(node);
            if (perm == null)
                return new BotResult("There does not exist a permission node by that text.");

            string s = "";
            foreach(var usr in BotDB.Users)
            {
                if(PermChecker.UserHasPerm(usr, perm))
                    s += $"- <@{usr.Id}>\r\n";
            }
            if (s == "")
                s = "No users have that permission.";
            await Context.Channel.SendMessageAsync(s, allowedMentions: AllowedMentions.None);
            return new BotResult();
        }

        [Command("effective")]
        [Summary("View your effective permissions")]
        public async Task ViewEffectiveOwn()
        {
            await printActualFor(Context.BotDbUser);
        }

        [Command("effective")]
        [Alias("effectiveo")]
        [Summary("View someone else's effective permissions")]
        [RequirePermission(Perms.Bot.Developer.ViewPermissions)]
        public async Task ViewEffectiveOther(BotDbUser u) => await printActualFor(u);

        [Command("check"), Alias("test")]
        [Summary("Checks whether you have the provided node")]
        public async Task Test(string nodeText)
        {
            await doCheckFor(Context.BotDbUser, nodeText);
        }

        [Command("check"), Alias("test", "checko", "testo")]
        [Summary("Checks whether another user has the provided node")]
        [RequirePermission(Perms.Bot.Developer.ViewPermissions)]
        public async Task Test(BotDbUser user, string nodeText)
        {
            await doCheckFor(user, nodeText);
        }
    
        [Command("grant")]
        [Summary("Gives or revokes the permission to or from the user")]
        [RequirePermission(Perms.Bot.All)]
        public async Task<RuntimeResult> GrantPerm(BotDbUser user, string nodeText)
        {
            var node = (NodeInfo)nodeText;
            if (!Context.HasPerm(node))
                return new BotResult("You must have the permission you wish to give.");
            if(PermChecker.UserHasPerm(user, node, out bool inherit))
            {
                if(!inherit)
                {
                    var i = user.Permissions.RemoveAll(x => x.PermNode.Type == PermType.Grant && x.PermNode.Node == node.Node);
                    await ReplyAsync($"Removed {i} perms that match the node.");
                    return new BotResult();
                }
            }
            user.WithPerm(new Perm(node, PermType.Grant));
            await printListFor(user);
            return new BotResult();
        }

        [Command("deny")]
        [Summary("Toggles denial of the permission")]
        [RequirePermission(Perms.Bot.All)]
        public async Task<RuntimeResult> DenyPerm(BotDbUser user, string nodeText)
        {
            var node = (NodeInfo)nodeText;
            if (!Context.HasPerm(node))
                return new BotResult("You must have the permission you wish to deny.");
            var i = user.Permissions.RemoveAll(x => x.PermNode.Type == PermType.Deny && x.PermNode.Node == node.Node);
            if(i > 0)
            {
                await ReplyAsync("Removed denial of permission.");
            } else
            {
                user.WithPerm(new Perm(node, PermType.Deny));
            }
            await printListFor(user);
            return new BotResult();
        }

        [Command("allow")]
        [Summary("Toggles allowal of the permission")]
        [RequirePermission(Perms.Bot.All)]
        public async Task<RuntimeResult> AllowPerm(BotDbUser user, string nodeText)
        {
            var node = (NodeInfo)nodeText;
            if (!Context.HasPerm(node))
                return new BotResult("You must have the permission you wish to allow.");
            var i = user.Permissions.RemoveAll(x => x.PermNode.Type == PermType.Allow && x.PermNode.Node == node.Node);
            if (i > 0)
            {
                await ReplyAsync("Removed allowal of permission.");
            }
            else
            {
                user.WithPerm(new Perm(node, PermType.Allow));
            }
            await printListFor(user);
            return new BotResult();
        }
    }
}
