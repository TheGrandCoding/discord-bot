using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiscordBot
{
    [Description("Base container for all permissions")]
    [AssignedBy(Perms.All)]
    public static partial class Perms
    {
        /// <summary>
        /// Base permission attribute
        /// </summary>
        public class PermissionAttribute : Attribute { }

        /// <summary>
        /// Specifies what permission can set this one
        /// </summary>
        public class AssignedByAttribute : PermissionAttribute
        {
            public string PermRequired { get; set; }
            public AssignedByAttribute(string perm) { PermRequired = perm; }
        }

        /// <summary>
        /// Indicates this permission cannot be given without the User also having the others specified
        /// </summary>
        public class MutuallyInclusive : PermissionAttribute
        {
            public string[] PermsRequired { get; set; }
            public MutuallyInclusive(params string[] perms) { PermsRequired = perms; }
        }

        /// <summary>
        /// Indicates this permission cannot be given if the User has any of the ones specified
        /// </summary>
        public class MutuallyExclusive : PermissionAttribute
        {
            public string[] PermsIllegal { get; set; }
            public MutuallyExclusive(params string[] reject) { PermsIllegal = reject; }
        }

        [Description("All permissions")]
        public const string All = "*";

        public static Dictionary<string, FieldInfo> AllPermissions { get; set; } = new Dictionary<string, FieldInfo>();

        static List<FieldInfo> findPerms(Type mainType)
        {
            var fields = (from f in mainType.GetFields()
                         where f.FieldType == typeof(string)
                         select f).ToList();
            foreach (var sub in mainType.GetNestedTypes())
                fields.AddRange(findPerms(sub));
            return fields;
        } 

        static Perms()
        {
            var fields = findPerms(typeof(Perms));
            foreach (var x in fields)
                AllPermissions[(string)x.GetValue(null)] = x;
        }
    
        static bool grantsPerm(string has, string wanted)
        {
            if (has == "*")
                return true;
            if (has == wanted)
                return true;
            var hasSplit = has.Split('.');
            var wantedSplit = wanted.Split('.');
            for (int i = 0; i < hasSplit.Length && i < wantedSplit.Length; i++)
            {
                if (hasSplit[i] == "*")
                    return true;
                if (hasSplit[i] != wantedSplit[i])
                    return false;
            }
            return false;
        }

        public static bool HasPerm(this BotUser user, string permission)
        {
            foreach(var perm in user.Permissions)
            {
                if (grantsPerm(perm, permission))
                    return true;
            }
            return false;
        }
    }
}
