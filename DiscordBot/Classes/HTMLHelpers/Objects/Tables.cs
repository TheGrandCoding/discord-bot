using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Table : DOMBase
    {
        public Table(string id = null, string cls = null) : base("table", id, cls) { }
    }

    public class TableElement : DOMBase
    {
        public TableElement(string tag, string content, string id = null, string cls = null) : base(tag, id, cls)
        {
            RawText = content;
        }
        public string ColSpan { get
            {
                return tagValues.GetValueOrDefault("colspan");
            } set
            {
                tagValues["colspan"] = value;
            }
        }
        public string RowSpan
        {
            get
            {
                return tagValues.GetValueOrDefault("rowspan");
            }
            set
            {
                tagValues["rowspan"] = value;
            }
        }
    }

    public class TableData : TableElement
    {
        public TableData(string content, string id = null, string cls = null) : base("td", content, id, cls)
        {
        }
    }

    public class TableHeader : TableElement
    {
        public TableHeader(string text, string id = null, string cls = null) : base("th", text, id, cls)
        {
        }
    }

    public class TableRow : DOMBase
    {
        public TableRow(string id = null, string cls = null) : base("tr", id, cls) { }

        public TableRow WithHeader(string text, string id = null, string cls = null)
        {
            Children.Add(new TableHeader(text, id, cls));
            return this;
        }
        public TableRow WithCell(string text, string id = null, string cls = null)
        {
            Children.Add(new TableData(text, id, cls));
            return this;
        }
    }
}
