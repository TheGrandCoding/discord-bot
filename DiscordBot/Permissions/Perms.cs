using DiscordBot.Classes;
using DiscordBot.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiscordBot
{
    #region Attributes
    public class Description : PermissionAttribute
    {
        public string Value { get; set; }
        public Description(string text)
        {
            Value = text;
        }
    }
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
    #endregion

    [Description("Description failed to fetch")]
    [AssignedBy(Perms.All)]
    public static partial class Perms
    {
        [Description("All permissions")]
        public const string All = "*";
    }
}
