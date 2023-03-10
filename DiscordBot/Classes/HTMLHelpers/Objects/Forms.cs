using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Form : DOMBase
    {
        public Form(string id = null, string cls = null) : base("form", id, cls)
        {
        }
        public void AddLabeledInput(string id, string labelText, string inputType, 
            string inputPlaceHolder = null, string inputValue = null,
            string onChange = null, string onInput = null)
        {
            var lbl = new Label(labelText)
                .WithTag("for", id);
            var inp = new Input(inputType, inputValue, id);
            if (inputPlaceHolder != null)
                inp.WithTag("placeholder", inputPlaceHolder);
            if (onChange != null)
                inp.WithTag("onchange", onChange);
            if (onInput != null)
                inp.WithTag("oninput", onInput);
            Children.Add(lbl);
            Children.Add(inp);
        }
    }
    public abstract class FormBase : DOMBase
    {
        public FormBase(string tag, string id = null, string cls = null) : base(tag, id, cls)
        {
        }

        public string Name { get => get("name"); set => set("name", value); }
        public string OnChange
        {
            get
            {
                return get("onchange");
            }
            set
            {
                set("onchange", value);
            }
        }
    }
    public class Input : FormBase
    {
        public string Type { get => get("type"); set => set("type", value); }
        public Input(string type, string value = null, string id = null, string cls = null)
            : base("input", id, cls)
        {
            Type = type;
            if (value != null)
                tagValues["value"] = value;
            IsOpenOnly = true;
        }

        public override bool ReadOnly
        {
            get
            {
                return get("readonly") == "";
            }
            set
            {
                set("readonly", value);
                if (value && Type == "checkbox")
                    OnClick = "return false;";
            }
        }

        public bool Checked
        {
            get
            {
                return get("checked") == "";
            }
            set
            {
                set("checked", value);
            }
        }
    }

    public class Select : FormBase
    {
        public Select(string id = null, string name = null, string cls = null) : base("select", id, cls)
        {
            tagValues["name"] = name;
        }
        public Select Add(string content, string value = null, bool selected = false, string id = null, string cls = null)
        {
            var opt = new Option(content, value, id, cls);
            if (selected)
                opt.WithTag("selected", "");
            Children.Add(opt);
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
