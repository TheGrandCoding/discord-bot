using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Anchor : HTMLBase
    {
        public Anchor(string href, string title = null, string id = null, string cls = null) : base("a", id, cls)
        {
            tagValues["href"] = href;
            tagValues["title"] = title;
        }
    }
}
