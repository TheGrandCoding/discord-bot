using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Header : DOMBase
    {
        public Header(int n, string text, string id = null, string cls = null) : base($"h{n}", id, cls) 
        {
            RawText = text;
        }
    }
    public class H1 : Header
    {
        public H1(string text, string id = null, string cls = null) : base(1, text, id, cls) { }
    }
    public class H2 : Header
    {
        public H2(string text, string id = null, string cls = null) : base(2, text, id, cls) { }
    }
    public class H3 : Header
    {
        public H3(string text, string id = null, string cls = null) : base(3, text, id, cls) { }
    }
}
