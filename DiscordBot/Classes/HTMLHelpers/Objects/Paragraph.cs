﻿using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Paragraph : DOMBase
    {
        public Paragraph(string content, string id = null, string cls = null) : base("p", id, cls)
        {
            RawText = content;
        }
    }

    public class Break : DOMBase
    {
        public Break(string id = null, string cls = null) : base("br", id, cls) { }
    }
}
