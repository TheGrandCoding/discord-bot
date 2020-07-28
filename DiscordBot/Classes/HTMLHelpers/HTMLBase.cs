using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace DiscordBot.Classes.HTMLHelpers
{
    public abstract class HTMLBase
    {
        public string Tag { get; protected set; }

        public string Id { get => get("id"); set => set("id", value); }
        public string Class { get => get("class"); set => set("class", value); }
        public string RawText { get; set; }
        protected Dictionary<string, string> tagValues { get; set; } = new Dictionary<string, string>();
        public HTMLBase(string tag, string id, string cls)
        {
            Tag = tag;
            Id = id;
            Class = cls;
            Children = new List<HTMLBase>();
        }
    
        public List<HTMLBase> Children { get; protected set; }
        
        protected virtual void WriteOpenTag(StringBuilder sb)
        {
            sb.Append($"<{Tag}");
            foreach(var keypair in tagValues)
            {
                var key = keypair.Key;
                var val = keypair.Value;
                if (val == "")
                    sb.Append(" " + key);
                else if (!string.IsNullOrWhiteSpace(val))
                    sb.Append($" {key}=\"{val}\"");
            }
            sb.Append(">");
        }
        protected string get(string thing) => tagValues.GetValueOrDefault(thing.ToLower());
        protected void set(string thing, string val) => tagValues[thing.ToLower()] = val;
        protected void set(string thing, bool val)
        {
            thing = thing.ToLower();
            if (val)
                set(thing, "");
            else
                tagValues.Remove(thing);
        }
        protected virtual void WriteCloseTag(StringBuilder sb)
        {
            sb.Append($"</{Tag}>");
        }

        protected virtual void WriteContent(StringBuilder sb) 
        {
            sb.Append(RawText);
        }

        public void Write(StringBuilder sb)
        {
            WriteOpenTag(sb);
            WriteContent(sb);
            foreach(var child in Children)
            {
                child.Write(sb);
            }
            WriteCloseTag(sb);
        }

        public HTMLBase WithTag(string name, string value)
        {
            tagValues[name] = value;
            return this;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            Write(sb);
            return sb.ToString();
        }

        public static implicit operator string(HTMLBase b) => b.ToString();
    }

    public abstract class DOMBase : HTMLBase
    {
        public DOMBase(string tag, string id, string cls) : base(tag, id, cls)
        {
        }

        public virtual bool ReadOnly {  get
            {
                return get("readonly") == "";
            } set
            {
                set("readonly", value);
            }
        }

        public string OnClick { get => get(nameof(OnClick)); set => set(nameof(OnClick), value); }
    }
}
