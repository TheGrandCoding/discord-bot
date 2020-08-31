using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot
{
    public class JsonReaderCreater : IToken
    {
        public string Name => Type;
        public string Type { get; }
        public Dictionary<string, IToken> Fields { get; set; } = new Dictionary<string, IToken>();

        public Dictionary<string, JsonReaderCreater> Objects { get; set; } = new Dictionary<string, JsonReaderCreater>();

        public void RegisterObj(JsonReaderCreater obj)
        {
            if (Parent != null)
            {
                Parent.RegisterObj(obj);
                return;
            }
            Objects[obj.Type] = obj;
        }

        public JsonReaderCreater Parent { get; }

        string _prefix;
        public string Prefix { get
            {
                return Parent?.Prefix ?? _prefix ?? "";
            } }

        public JsonReaderCreater(string name, JObject obj, JsonReaderCreater parent = null, string type = null, string prefix = null)
        {
            _prefix = prefix;
            Parent = parent;
            Type = Prefix + (type ?? name ?? "_unknown_");
            if (parent != null)
                parent.RegisterObj(this);
            foreach(var keypair in obj)
            {
                if(Fields.TryGetValue(keypair.Key, out var val))
                {
                    if(val == null)
                    {
                        Fields[keypair.Key] = getToken(keypair.Key, keypair.Value);
                    }
                } else
                {
                    Fields[keypair.Key] = getToken(keypair.Key, keypair.Value);
                }
            }
        }

        public IToken getToken(string name, JToken token)
        {
            if (token.Type == JTokenType.String)
                return new BasicToken(name, "string");
            if (token.Type == JTokenType.Integer)
                return new BasicToken(name, "int");
            if (token.Type == JTokenType.Float)
                return new BasicToken(name, "float");
            if (token.Type == JTokenType.Boolean)
                return new BasicToken(name, "bool");
            if (token.Type == JTokenType.Array)
                return new ArrayToken(name, this, token as JArray);
            if (token.Type == JTokenType.Object)
                return new JsonReaderCreater(name.Substring(0, 1).ToUpper() + name[1..], token as JObject, this);
            return null;
        }

        public void GetModelString(StringBuilder sb)
        {
            string intf = Prefix == "API" ? " : IModel" : "";
            sb.Append($"public class {Name}{intf}\r\n{{\r\n");
            foreach(var field in Fields)
            {

                sb.Append($"    public {(field.Value?.Type ?? "object")} {field.Key}" +  " { get; set; }\r\n");
            }
            sb.Append("}\r\n");
            if(Objects.Count > 0)
            {
                sb.Append("\r\n#region Classes used within this JSON object.\r\n");
                foreach(var child in Objects)
                {
                    child.Value.GetModelString(sb);
                }
                sb.Append("\r\n#endregion\r\n");
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            GetModelString(sb);
            return sb.ToString();
        }
    }

    public interface IToken
    {
        public string Name { get; }
        public string Type { get; }
    }
    public class BasicToken : IToken
    {
        public string Name { get; }
        public string Type { get; }
        public BasicToken(string name, string type)
        {
            Name = name;
            Type = type ?? "object";
        }
    }
    public class ArrayToken : IToken
    {
        public string Name { get; }
        public string Type { get; }
        public List<IToken> Tokens { get; }
        public ArrayToken(string name, JsonReaderCreater parent, JArray array)
        {
            Name = name;
            Tokens = new List<IToken>();
            foreach(var obj in array)
            {
                var t = parent.getToken(name, obj);
                if (t != null)
                {
                    if (t is JsonReaderCreater jrc)
                        parent.RegisterObj(jrc);
                    Tokens.Add(t);
                }
                if (Tokens.Count >= 3 && Tokens.Distinct().Count() == 1)
                    break;
            }
            var l = Tokens.Select(x => x.Type).Distinct().ToList();
            if (l.Count == 1)
                Type = $"List<{l[0]}>";
            else
                Type = $"List<object>";
        }
    }
}
