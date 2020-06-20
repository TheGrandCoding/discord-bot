using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Label : HTMLBase
    {
        public Label(string text, string id = null, string cls = null) : base("label", id, cls)
        {
            RawText = text;
        }
    }
}
