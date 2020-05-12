using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers
{
    public class HTMLThing : IDisposable
    {
        private string _tag;
        protected StringBuilder _sb;
        public HTMLThing(StringBuilder sb, string tagName, string id, string classValue)
        {
            _sb = sb;
            _tag = tagName;
            WriteOpenTag(id, classValue);
        }
        protected virtual void WriteOpenTag(string id, string clsV)
        {
            _sb.Append($"<{_tag} id=\"{id}\" class=\"{clsV}\">");
        }
        protected virtual void WriteCloseTag()
        {
            _sb.Append($"</{_tag}>");
        }
        public void Dispose()
        {
            WriteCloseTag();
        }
    }
    public class HTMLTable : HTMLThing
    {
        public HTMLTable(StringBuilder sb, string id = "default", string classValue = "") 
            : base (sb, "table", id, classValue) { }

        public HTMLRow AddRow(string id = "default", string clsV = "")
            => new HTMLRow(_sb, id, clsV);
        public HTMLRow AddHeaderRow(string id = "default", string clsV = "")
            => new HTMLRow(_sb, id, clsV, true);
    }
    public class HTMLRow : HTMLThing
    {
        bool _header;
        public HTMLRow(StringBuilder sb, string id, string classValue, bool isHeader = false) : base(sb, "tr", id, classValue)
        {
            _header = isHeader;
        }
        protected override void WriteOpenTag(string id, string clsV)
        {
            if (_header)
                _sb.Append("<thead>");
            base.WriteOpenTag(id, clsV);
        }
        protected override void WriteCloseTag()
        {
            base.WriteCloseTag();
            if (_header)
                _sb.Append("</thead>");
        }

        public HTMLCell AddCell(string content, string id = "default", string clsV = "")
            => new HTMLCell(_sb, "td", id, clsV).Write(content);
        public HTMLCell AddHeaderCell(string content, string id = "default", string clsV = "")
            => new HTMLCell(_sb, "th", id, clsV).Write(content);
    }

    public class HTMLCell : HTMLThing
    {
        public HTMLCell(StringBuilder sb, string type, string id, string clsV) : base(sb, type, id, clsV)
        {
        }
        public HTMLCell Write(string s)
        {
            _sb.Append(s);
            return this;
        }
    }
}
