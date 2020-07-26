using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public abstract class FormBase : DOMBase
    {
        public FormBase(string tag, string id = null, string cls = null) : base(tag, id, cls)
        {
        }

        public string Name { get => get("name"); set => set("name", value); }
    }
    public class Input : FormBase
    {
        public Input(string type, string value = null, string id = null, string cls = null)
            : base("input", id, cls)
        {
            tagValues["type"] = type;
            if (value != null)
                tagValues["value"] = value;
        }
    }

    public class Select : FormBase
    {
        public Select(string id = null, string name = null, string cls = null) : base("select", id, cls)
        {
            tagValues["name"] = name;
        }
        public Select Add(string content, string value = null, string id = null, string cls = null)
        {
            Children.Add(new Option(content, value, id, cls));
            return this;
        }
        public Select AddGroup(string label, string id = null, string cls = null)
        {
            Children.Add(new OptionGroup(label, id, cls));
            return this;
        }
    }

    public class Option : DOMBase
    {
        public Option(string content, string value = null, string id = null, string cls = null) : base("option", id, cls)
        {
            RawText = content;
            tagValues["value"] = value;
        }
    }

    public class OptionGroup : DOMBase
    {
        public string Label { get => get("label"); set => set("label", value); }
        public OptionGroup(string label, string id =null, string cls = null) : base("optgroup", id, cls)
        {
            Label = label;
        }
        public OptionGroup Add(string content, string value = null, string id = null, string cls = null)
        {
            Children.Add(new Option(content, value, id, cls));
            return this;
        }
        public OptionGroup AddGroup(string label, string id = null, string cls = null)
        {
            Children.Add(new OptionGroup(label, id, cls));
            return this;
        }
    }
}
