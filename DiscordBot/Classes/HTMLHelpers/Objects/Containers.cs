using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Div : DOMBase
    {
        public Div(string id = null, string cls = null) : base("div", id, cls) { }
    }
    public class Span : DOMBase
    {
        public Span(string id = null, string cls = null) : base("span", id, cls) { }
    }
}
