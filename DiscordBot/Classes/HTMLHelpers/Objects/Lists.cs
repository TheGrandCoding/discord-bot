using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class ListItem : HTMLBase
    {
        public ListItem(string content, string id = null, string cls = null) : base("li", id, cls)
        {
            RawText = content;
        }
    }
    public class UnorderedList : HTMLBase
    {
        public UnorderedList(string id = null, string cls = null) : base("ul", id, cls)
        {
        }
        public void AddItem(string item)
        {
            this.Children.Add(new ListItem(item));
        }
    }
    public class OrderedList : HTMLBase
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
