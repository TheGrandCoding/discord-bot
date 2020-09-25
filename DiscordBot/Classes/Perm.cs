using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Commands;
using DiscordBot.MLAPI;
using DiscordBot.Permissions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using static DiscordBot.Perms;

namespace DiscordBot.Classes
{
    [JsonConverter(typeof(JsonPermConverter))]
    public class Perm : NodeInfo
    {
        public string RawNode {  get
            {
                string n = "";
                if (Type == PermType.Allow)
                    n = "+";
                else if (Type == PermType.Deny)
                    n = "-";
                return n + Node;
            } }

        public PermType Type { get; set; }

        public Perm(FieldInfo info) : base(null, null)
        {
            var x = new FieldNodeInfo(info);
            Node = x.Node;
            Description = x.Description;
            Type = PermType.Grant;
        }
        public Perm(NodeInfo node, PermType type) : base(node.Node, node.Description)
        {
            Type = type;
        }

        public static Perm Parse(string n) => new Perm(n);

        private Perm(string node) : base(null, null)
        {
            if(node.StartsWith('+'))
            {
                Node = node[1..];
                Type = PermType.Allow;
            } else if (node.StartsWith('-'))
            {
                Node = node[1..];
                Type = PermType.Deny;
            } else
            {
                Node = node;
                Type = PermType.Grant;
            }
        }
    
        public bool isMatch(NodeInfo seeking, out bool inherited)
        {
            inherited = false;
            if (this.Node == seeking.Node)
                return true;
            if (this.Node == Perms.All)
                return false;
            inherited = false;
            var hasSplit = this.Node.Split('.');
            var wantedSplit = seeking.Node.Split('.');
            for (int i = 0; i < hasSplit.Length && i < wantedSplit.Length; i++)
            {
                if (hasSplit[i] == "*")
                {
                    inherited = true;
                    return true;
                } else if (hasSplit[i] != wantedSplit[i])
                {
                    return false;
                }
            }
            return false;
        }
    }

    public class JsonPermConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Perm);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return Perm.Parse((string)reader.Value);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var perm = (Perm)value;
            var jval = new JValue(perm.RawNode);
            jval.WriteTo(writer);
        }
    }

    public enum PermType
    {
        /// <summary>
        /// Highest priority provide
        /// </summary>
        Allow,
        /// <summary>
        /// Denial of permission
        /// </summary>
        Deny,
        /// <summary>
        /// Lowest priority provide
        /// </summary>
        Grant
    }
}
