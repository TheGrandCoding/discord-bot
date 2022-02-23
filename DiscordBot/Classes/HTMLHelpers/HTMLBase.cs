using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public abstract class HTMLBase
    {
        private string DebuggerDisplay => ToString(true);

        public string Tag { get; protected set; }

        private List<string> m_classes = new List<string>();

        public string Id { get => get("id"); set => set("id", value); }
        public string Class { get => string.Join(' ', m_classes); set => m_classes = (value ?? "").Split(' ').ToList(); }

        public List<string> ClassList { get => m_classes; }

        public string RawText { get; set; }
        protected Dictionary<string, string> tagValues { get; set; } = new Dictionary<string, string>();
        public HTMLBase(string tag, string id, string cls)
        {
            Tag = tag;
            tagValues["class"] = null;
            Id = id;
            Class = cls;
            Children = new List<HTMLBase>();
        }

        public List<HTMLBase> Children { get; protected set; }

        protected virtual void WriteOpenTag(StringBuilder sb, int tab = -1)
        {
            if (tab > -1)
                sb.Append(new string(' ', tab * 4));
            sb.Append($"<{Tag}");
            if (!string.IsNullOrWhiteSpace(Class))
                set("class", Class);
            foreach (var keypair in tagValues)
            {
                var key = keypair.Key;
                var val = keypair.Value;
                if (val == "")
                    sb.Append(" " + key);
                else if (!string.IsNullOrWhiteSpace(val))
                    sb.Append($" {key}=\"{val}\"");
            }
            sb.Append(">");
            if (tab > -1)
                sb.Append("\r\n");
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
        protected virtual void WriteCloseTag(StringBuilder sb, int tab = -1)
        {
            if (tab > -1)
                sb.Append(new string(' ', tab * 4));
            sb.Append($"</{Tag}>");
            if (tab > -1)
                sb.Append("\r\n");
        }

        protected virtual void WriteContent(StringBuilder sb, int tab = -1)
        {
            if (RawText == null)
                return;
            if (tab > -1)
                sb.Append(new string(' ', tab * 4));
            sb.Append(RawText);
            if (tab > -1)
                sb.Append("\r\n");
        }

        public void Write(StringBuilder sb, int tab = -1)
        {
            WriteOpenTag(sb, tab);
            WriteContent(sb, tab == -1 ? -1 : tab + 1);
            foreach (var child in Children)
            {
                child.Write(sb, tab == -1 ? -1 : tab + 1);
            }
            WriteCloseTag(sb, tab);
        }

        public HTMLBase WithTag(string name, string value)
        {
            tagValues[name] = value;
            return this;
        }
        public HTMLBase WithRawText(string content)
        {
            RawText = content;
            return this;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            Write(sb);
            return sb.ToString();
        }
        public string ToString(bool withFormatting)
        {
            if (!withFormatting)
                return ToString();
            var sb = new StringBuilder();
            Write(sb, 0);
            return sb.ToString();
        }

        public static implicit operator string(HTMLBase b) => b.ToString();
        public static HTMLBase operator +(HTMLBase left, HTMLBase right)
        {
            left.Children.Add(right);
            return left;
        }
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
        public virtual bool Disabled
        {
            get
            {
                return get("disabled") == "";
            }
            set
            {
                set("disabled", value);
            }
        }

        public string Style { get => get(nameof(Style)); set => set(nameof(Style), value); }

        public string OnClick { get => get(nameof(OnClick)); set => set(nameof(OnClick), value); }
        public string OnMouseOver { get => get(nameof(OnMouseOver)); set => set(nameof(OnMouseOver), value); }
        public string OnMouseEnter { get => get(nameof(OnMouseEnter)); set => set(nameof(OnMouseEnter), value); }
        public string OnMouseLeave { get => get(nameof(OnMouseLeave)); set => set(nameof(OnMouseLeave), value); }
        public string OnMouseOut { get => get(nameof(OnMouseOut)); set => set(nameof(OnMouseOut), value); }
    }
}
