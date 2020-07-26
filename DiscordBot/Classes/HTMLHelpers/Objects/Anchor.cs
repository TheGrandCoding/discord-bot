using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Anchor : DOMBase
    {
        public Anchor(string href, string text = null, string title = null, string id = null, string cls = null) : base("a", id, cls)
        {
            tagValues["href"] = href;
            tagValues["title"] = title;
            RawText = text ?? href;
        }
    }
}
