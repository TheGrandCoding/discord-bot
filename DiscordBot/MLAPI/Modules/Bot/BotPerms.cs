﻿using DiscordBot.Classes;
using DiscordBot.Permissions;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using static DiscordBot.Perms;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{
    public class BotPerms : AuthedAPIBase
    {
        public PermissionsService Service { get; set; }
        public BotPerms(APIContext c) : base(c, "bot") 
        {
            Service = Context.Services.GetRequiredService<PermissionsService>();
        }

        (bool can, string err) canSetPermission(BotDbUser oper, BotDbUser target, NodeInfo node, NodeInfo requires)
        {
            if (oper == null)
                return (false, "Operator is null");
            if (!PermChecker.UserHasPerm(oper, node))
                return (false, "You must have the permission yourself");
            if (!PermChecker.UserHasPerm(oper, requires))
                return (false, $"Must have '{requires.Description}' to change");
            return (true, null);
        }

        string buildHTML(BotDbUser user, FieldInfo field)
        {
            string node = (string)field.GetValue(null);
            var perm = Service.FindNode(node);
            bool has = PermChecker.UserHasPerm(user, perm, out bool d);
            var requires = Service.FindNode(perm.GetAttribute<AssignedByAttribute>().PermRequired);
            (bool canChange, _) = canSetPermission(Context.User, user, perm, requires);
            string item = $"<label id='{node}' data-change='{requires.Node}' onmouseout='nohover(this);' onmouseover='hoverp(this);'><input {((d || !canChange) ? "disabled" : "")} class='{(has ? "inp-has" : "")} {(d ? "inp-dis" : "")}' type='checkbox' id='cb_{node}' onclick='changep(this);' {(has ? "checked" : "")}/> {perm.Description}";
            return item + "</label><br/>";
        }

        string buildHTML(BotDbUser user, Type type, int level)
        {
            string html = $"<div class='level-{level}'>";
            html += $"<p>{type.Name}</p>";
            var fields = from f in type.GetFields()
                         where f.FieldType == typeof(string)
                         select f;
            foreach (var field in fields)
                html += buildHTML(user, field);
            foreach (var sibling in type.GetNestedTypes())
            {
                html += buildHTML(user, sibling, level + 1);
            }
            return html += "</div>";
        }

        string buildHTML(BotDbUser user)
        {
            var type = typeof(DiscordBot.Perms);
            return buildHTML(user, type, 0);
        }

        async Task permsFor(BotDbUser user)
        {
            string p = buildHTML(user);
            await ReplyFile("permissions.html", 200, new Replacements()
                .Add("perms", p)
                .Add("usr", user.Id)
                .Add("name", user.Name));
        }

        [Method("GET"), Path("/bot/permissions")]
        public async Task GetPermissionsSelf()
        {
            await permsFor(Context.User);
        }

        [Method("GET"), Path("/bot/permissions")]
        [RequirePermNode(Perms.Bot.Developer.ViewPermissions)]
        public async Task SeePermissions(uint user)
        {
            var usr = Context.BotDB.GetUserAsync(user).Result;
            if (usr == null)
            {
                await HTTPError(System.Net.HttpStatusCode.NotFound, "User", "Unknown user id");
                return;
            }
            await permsFor(usr);
        }

        [Method("POST"), Path("/bot/permissions")]
        public async Task TrySetPermission(uint user, string node, bool value)
        {
            var other = Context.BotDB.GetUserAsync(user).Result;
            if (other == null)
            {
                await RespondRaw("Unknown target", 404);
                return;
            }
            var perm = Perm.Parse(node);
            if (perm == null)
            {
                await RespondRaw("Unknown permission", 404);
                return;
            }
            var requires = Service.FindNode(perm.GetAttribute<AssignedByAttribute>().PermRequired);
            (bool can, string errorReason) = canSetPermission(Context.User, other, perm, requires);
            if (can)
            {
                try
                {
                    if (value)
                        other.WithPerm(perm);
                    else
                        other.RemovePerm(perm);
                    await RespondRaw("Set");
                }
                catch (Exception ex)
                {
                    await RespondRaw("Failed: " + ex.Message, 500);
                }
            }
            else
            {
                await RespondRaw($"Failed: {errorReason}", 400);
            }
        }
    }
}
