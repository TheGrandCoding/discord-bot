using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Code : DOMBase
    {
        public Code(string text, string id = null, string cls = null) : base("code", id, cls)
        {
            RawText = text;
        }
    }
    public class Pre : DOMBase
    {
        public Pre(string text, string id = null, string cls = null) : base("pre", id, cls)
        {
            RawText = text;
        }
    }
}
