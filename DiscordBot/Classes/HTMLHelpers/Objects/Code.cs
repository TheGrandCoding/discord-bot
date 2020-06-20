using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Code : HTMLBase
    {
        public Code(string text, string id = null, string cls = null) : base("code", id, cls)
        {
            RawText = text;
        }
    }
    public class Pre : HTMLBase
    {
        public Pre(string text, string id = null, string cls = null) : base("pre", id, cls)
        {
            RawText = text;
        }
    }
}
