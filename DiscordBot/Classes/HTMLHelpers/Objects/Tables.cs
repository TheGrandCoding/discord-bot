using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Table : DOMBase
    {
        public Table(string id = null, string cls = null) : base("table", id, cls) { }

        public TableRow HeaderRow
        {
            get
            {
                return (TableRow)this.Children.FirstOrDefault(x => x.Tag == "tr");
            }
        }

        public Table WithHeaderColumn(string text)
            => WithHeaderColumn(new RawObject(text));
        public Table WithHeaderColumn(HTMLBase html)
        {
            if (HeaderRow == null)
                this.Children.Add(new TableRow());
            HeaderRow.Children.Add(new TableHeader(null)
            {
                Children = { html }
            });
            return this;
        }

        public Table WithRow(params object[] cells)
        {
            var ls = new List<HTMLBase>();
            foreach(var x in cells)
            {
                if (x is string y)
                    ls.Add(new RawObject(y));
                else if (x is HTMLBase)
                    ls.Add(x as HTMLBase);
                else
                    throw new ArgumentException($"{x.GetType().Name} cannot be used", nameof(cells));
            }
            return WithRow(ls.ToArray());
        }
        public Table WithRow(params HTMLBase[] cells)
        {
            var row = new TableRow();
            foreach (var x in cells)
                row.Children.Add(new TableData(null) { Children = { x } });
            Children.Add(row);
            return this;
        }
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
            Children.Add(new TableData(text.Replace("<", "&lt;").Replace(">", "&gt;"), id, cls));
            return this;
        }
    }
}
