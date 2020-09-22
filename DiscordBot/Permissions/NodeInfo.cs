using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DiscordBot.Permissions
{
    public class NodeInfo
    {
        public NodeInfo(FieldInfo info)
        {
            Field = info;
            if(info != null)
                Node = (string)info.GetValue(null);
        }

        public string Node { get; set; }
        public FieldInfo Field { get; set; }
        public string Description => GetAttribute<Description>().Value;

        T getAttrInParent<T>(Type parent) where T : PermissionAttribute
        {
            if (parent == null)
                return null;
            return parent.GetCustomAttribute<T>() ?? getAttrInParent<T>(parent.DeclaringType);
        }

        public T GetAttribute<T>(bool inherit = true) where T : PermissionAttribute
        {
            var attr = Field.GetCustomAttribute<T>();
            if (attr == null && inherit)
                attr = getAttrInParent<T>(Field.DeclaringType);
            return attr;
        }

        public bool HasAttr<T>(bool inherit = true) where T : PermissionAttribute
        {
            return GetAttribute<T>(inherit) != null;
        }

        public static implicit operator string(NodeInfo i) => i.Node;
        public static implicit operator NodeInfo(string s)
        {
            var n = Perms.Parse(s);
            if (n == null)
                throw new InvalidCastException($"Could not parse `{s}` as any permissions node");
            return n;
        }
}
    }
