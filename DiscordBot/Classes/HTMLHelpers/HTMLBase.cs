using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers
{
    public abstract class HTMLBase
    {
        public string Tag { get; protected set; }
        public string Id => tagValues["id"];
        public string Class => tagValues["class"];
        public string RawText { get; set; }
        protected Dictionary<string, string> tagValues { get; set; }
        public HTMLBase(string tag, string id, string cls)
        {
            Tag = tag;
            tagValues = new Dictionary<string, string>()
            {
                {"id", id },
                {"class", cls }
            };
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
                if (!string.IsNullOrWhiteSpace(val))
                    sb.Append($" {key}=\"{val}\"");
            }
            sb.Append(">");
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

        public override string ToString()
        {
            var sb = new StringBuilder();
            Write(sb);
            return sb.ToString();
        }

        public static implicit operator string(HTMLBase b) => b.ToString();
    }
}
