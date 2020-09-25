using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;

namespace DiscordBot.Permissions
{
    public class NodeInfo
    {
        public string Node { get; set; }
        public virtual string Description { get; set; }

        public List<PermissionAttribute> Attributes { get; set; }

        public virtual T GetAttribute<T>() where T : PermissionAttribute
        {
            var attr = (T)Attributes.FirstOrDefault(x => x is T);
            return attr;
        }

        public bool HasAttr<T>() where T : PermissionAttribute
        {
            return GetAttribute<T>() != null;
        }

        public NodeInfo(string node, string d)
        {
            Node = node;
            Description = d;
            Attributes = new List<PermissionAttribute>();
        }

        public NodeInfo SetAssignedBy(string n)
        {
            Attributes.Add(new AssignedByAttribute(n));
            return this;
        }

        static PermissionsService service;
        public static implicit operator string(NodeInfo i) => i.Node;
        public static implicit operator NodeInfo(string n)
        {
            service ??= Program.Services.GetRequiredService<PermissionsService>();
            return service.FindNode(n);
        }
    }
    public class FieldNodeInfo : NodeInfo
    {
        public FieldNodeInfo(FieldInfo info) : base(null, null)
        {
            Field = info;
            if(info != null)
                Node = (string)info.GetValue(null);
            Attributes = info?.GetCustomAttributes<PermissionAttribute>(true).ToList() ?? new List<PermissionAttribute>();
        }

        public override string Description
        {
            get
            {
                return GetAttribute<Description>().Value;
            } set
            {
            }
        }
        public FieldInfo Field { get; set; }
    }
}
