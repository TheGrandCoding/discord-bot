using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Header : HTMLBase
    {
        public Header(int n, string id = null, string cls = null) : base($"h{n}", id, cls) { }
    }
    public class H1 : Header
    {
        public H1(string id = null, string cls = null) : base(1, id, cls) { }
    }
    public class H2 : Header
    {
        public H2(string id = null, string cls = null) : base(2, id, cls) { }
    }
    public class H3 : Header
    {
        public H3(string id = null, string cls = null) : base(3, id, cls) { }
    }
}
