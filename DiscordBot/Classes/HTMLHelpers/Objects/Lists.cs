using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class ListItem : DOMBase
    {
        public ListItem(string content, string id = null, string cls = null) : base("li", id, cls)
        {
            RawText = content;
        }
    }
    public class UnorderedList : DOMBase
    {
        public UnorderedList(string id = null, string cls = null) : base("ul", id, cls)
        {
        }
        public UnorderedList AddItem(string item)
        {
            this.Children.Add(new ListItem(item));
            return this;
        }
    }
    public class OrderedList : DOMBase
    {
        public OrderedList(string id = null, string cls = null) : base("ol", id, cls)
        {
        }
        public void AddItem(string item)
        {
            this.Children.Add(new ListItem(item));
        }
    }
}
