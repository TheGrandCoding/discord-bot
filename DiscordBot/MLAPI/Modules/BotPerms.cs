﻿using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using static DiscordBot.Perms;

namespace DiscordBot.MLAPI.Modules
{
    public class BotPerms : APIBase
    {
        public BotPerms(APIContext c) : base(c, "bot") { }

        (bool can, string err) canSetPermission(BotUser oper, BotUser target, Perm node, Perm requires)
        {
            if (oper == null)
                return (false, "Operator is null");
            if (!node.UserHasPerm(oper))
                return (false, "You must have the permission yourself");
            if (!requires.UserHasPerm(oper))
                return (false, $"Must have '{requires.Description}' to change");
            var userMustHave = node.GetAttribute<MutuallyInclusive>();
            if(userMustHave != null)
            {
                foreach(var x in userMustHave.PermsRequired.Select(x => Perms.Parse(x)))
                {
                    if (!x.UserHasPerm(target))
                        return (false, $"User must have '{x.Description}' before recieving this permission");
                }
            }
            var userProhibits = node.GetAttribute<MutuallyExclusive>();
            if(userProhibits != null)
            {
                foreach(var x in userProhibits.PermsIllegal.Select(x => Perms.Parse(x)))
                {
                    if (x.UserHasPerm(target))
                        return (false, $"User must not have '{x.Description}'");
                }
            }
            return (true, null);
        }

        string buildHTML(BotUser user, FieldInfo field)
        {
            string node = (string)field.GetValue(null);
            Perm perm = Perms.Parse(node);
            if (perm.HasAttr<NotWebModifiable>())
                return "";
            bool has = perm.UserHasPerm(user, out bool d);
            var requires = Perms.Parse(perm.GetAttribute<AssignedByAttribute>().PermRequired);
            (bool canChange, _) = canSetPermission(Context.User, user, perm, requires);
            string item = $"<label id='{node}' data-change='{requires.RawNode}' onmouseout='nohover(this);' onmouseover='hoverp(this);'><input {((d || !canChange) ? "disabled" : "")} class='{(has ? "inp-has" : "")} {(d ? "inp-dis" : "")}' type='checkbox' id='cb_{node}' onclick='changep(this);' {(has ? "checked" : "")}/> {perm.Description}";
            return item + "</label><br/>";
        }

        string buildHTML(BotUser user, Type type, int level)
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

        string buildHTML(BotUser user)
        {
            var type = typeof(DiscordBot.Perms);
            return buildHTML(user, type, 0);
        }

        void permsFor(BotUser user)
        {
            string p = buildHTML(user);
            ReplyFile("permissions.html", 200, new Replacements()
                .Add("perms", p)
                .Add("usr", user.Id)
                .Add("name", user.Name));
        }

        [Method("GET"), Path("/bot/permissions")]
        public void GetPermissionsSelf()
        {
            permsFor(Context.User);
        }

        [Method("GET"), Path("/bot/permissions")]
        [RequirePermNode(Perms.Bot.Developer.ViewPermissions)]
        public void SeePermissions(ulong user)
        {
            var usr = Program.GetUserOrDefault(user);
            if (usr == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "User", "Unknown user id");
                return;
            }
            permsFor(usr);
        }

        [Method("POST"), Path("/bot/permissions")]
        public void TrySetPermission(ulong user, string node, bool value)
        {
            var other = Program.GetUserOrDefault(user);
            if (other == null)
            {
                RespondRaw("Unknown target", 404);
                return;
            }
            var perm = Perms.Parse(node);
            if (perm == null || perm.HasAttr<NotWebModifiable>() || perm.Field == null)
            {
                RespondRaw("Unknown permission", 404);
                return;
            }
            var requires = Perms.Parse(perm.GetAttribute<AssignedByAttribute>().PermRequired);
            (bool can, string errorReason) = canSetPermission(Context.User, other, perm, requires);
            if (can)
            {
                try
                {
                    if (value)
                        other.Permissions.Add(perm);
                    else
                        other.Permissions.Remove(perm);
                    Program.Save();
                    RespondRaw("Set");
                }
                catch (Exception ex)
                {
                    RespondRaw("Failed: " + ex.Message, 500);
                }
            }
            else
            {
                RespondRaw($"Failed: {errorReason}", 400);
            }
        }
    }
}
