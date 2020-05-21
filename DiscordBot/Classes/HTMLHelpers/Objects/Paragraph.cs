using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Paragraph : HTMLBase
    {
        public Paragraph(string content, string id = null, string cls = null) : base("p", id, cls)
        {
            RawText = content;
        }
    }
}
