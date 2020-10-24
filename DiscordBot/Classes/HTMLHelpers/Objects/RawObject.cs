using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class RawObject : DOMBase
    {
        public RawObject(string text) : base(null, null, null)
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
        protected override void WriteContent(StringBuilder sb, int tab = -1)
        {
            if (tab > -1)
                sb.Append(new string(' ', tab * 4));
            sb.Append(RawText);
            if(tab > -1)
                sb.Append("\r\n");
        }
        protected override void WriteCloseTag(StringBuilder sb, int tab = -1)
        {
        }

        public static implicit operator RawObject(string text)
        {
            return new RawObject(text);
        }
        public static implicit operator string(RawObject text)
        {
            return text.RawText;
        }
    }
}
