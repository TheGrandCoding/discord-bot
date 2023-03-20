using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class RawText : DOMBase
    {
        public RawText(string text) : base(null, null, null)
        {
            RawText = text;
        }
        protected override void WriteOpenTag(StringBuilder sb, int tab = -1)
        {
            if (tab > -1)
            {
                sb.Append(new string(' ', tab * 4));
                sb.Append("\r\n");
            }
        }
        protected override void WriteCloseTag(StringBuilder sb, int tab = -1)
        {
        }

        public static implicit operator RawText(string text)
        {
            return new RawText(text);
        }
        public static implicit operator string(RawText text)
        {
            return text.RawText;
        }
    }
    public class RawHTML : DOMBase
    {
        public RawHTML(string html) : base(null, null, null)
        {
            RawHTML = html;
        }
        protected override void WriteOpenTag(StringBuilder sb, int tab = -1)
        {
            if (tab > -1)
            {
                sb.Append(new string(' ', tab * 4));
                sb.Append("\r\n");
            }
        }
        protected override void WriteCloseTag(StringBuilder sb, int tab = -1)
        {
        }
    }
}
