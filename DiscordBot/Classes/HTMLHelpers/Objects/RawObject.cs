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
        protected override void WriteOpenTag(StringBuilder sb)
        {
        }
        protected override void WriteContent(StringBuilder sb)
        {
            sb.Append(RawText);
        }
        protected override void WriteCloseTag(StringBuilder sb)
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
