using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class StrongText : DOMBase
    {
        public StrongText(string content = null, string id = null, string cls = null) : base("strong", id, cls)
        {
            RawText = content;
        }
    }
    public class EmphasisText : DOMBase
    {
        public EmphasisText(string content, string id = null, string cls = null) : base("em", id, cls)
        {
            RawText = content;
        }
    }
    public class ItalicText : DOMBase
    {
        public ItalicText(string content, string id = null, string cls = null) : base("i", id, cls)
        {
            RawText = content;
        }
    }
}
