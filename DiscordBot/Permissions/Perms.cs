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

        public static Dictionary<string, Perm> AllPermissions { get; set; } = new Dictionary<string, Perm>();

        static List<FieldInfo> findPerms(Type mainType)
        {
            var fields = (from f in mainType.GetFields()
                         where f.FieldType == typeof(string)
                         select f).ToList();
            foreach (var sub in mainType.GetNestedTypes())
                fields.AddRange(findPerms(sub));
            return fields;
        } 

        public static Perm Parse(string n)
        {
            AllPermissions.TryGetValue(n, out var p);
            return p;
        }

        static Perms()
        {
            var fields = findPerms(typeof(Perms));
            foreach (var x in fields)
            {
                var perm = new Perm(x);
                AllPermissions[perm.RawNode] = perm;
            }
        }
    }
}
