using DiscordBot.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiscordBot.Services
{
    public class PermissionsService : Service
    {
        public Dictionary<string, NodeInfo> AllNodes { get; set; } = new Dictionary<string, NodeInfo>();

        public PermissionsService()
        {
            var fields = findPerms(typeof(Perms));
            foreach(var x in fields)
            {
                var node = new FieldNodeInfo(x);
                AllNodes[node.Node] = node;
            }
        }

        static List<FieldInfo> findPerms(Type mainType)
        {
            var fields = (from f in mainType.GetFields()
                          where f.FieldType == typeof(string)
                          select f).ToList();
            foreach (var sub in mainType.GetNestedTypes())
                fields.AddRange(findPerms(sub));
            return fields;
        }

        public void RegisterNewNode(NodeInfo n)
        {
            if (AllNodes.ContainsKey(n.Node))
                throw new ArgumentException("Impossible to override existing permission.");
            AllNodes[n.Node] = n;
        }

        public NodeInfo FindNode(string n)
        {
            AllNodes.TryGetValue(n, out var p);
            return p;
        }
    }
}
