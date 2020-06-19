using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class StrongText : HTMLBase
    {
        public StrongText(string content, string id = null, string cls = null) : base("strong", id, cls)
        {
            RawText = content;
        }
    }
    public class EmphasisText : HTMLBase
    {
        public EmphasisText(string content, string id = null, string cls = null) : base("em", id, cls)
        {
            RawText = content;
        }
    }
}
