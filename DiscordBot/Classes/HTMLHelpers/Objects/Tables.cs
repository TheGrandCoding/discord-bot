using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Table : HTMLBase
    {
        public Table(string id = null, string cls = null) : base("table", id, cls) { }
    }

    public class TableData : HTMLBase
    {
        public TableData(string content, string id = null, string cls = null) : base("td", id, cls)
        {
            RawText = content;
        }
    }

    public class TableHeader : HTMLBase
    {
        public TableHeader(string text, string id = null, string cls = null) : base("th", id, cls)
        {
            RawText = text;
        }
    }

    public class TableRow : HTMLBase
    {
        public TableRow(string id = null, string cls = null) : base("tr", id, cls) { }
    }
}
