using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Input : HTMLBase
    {
        public Input(string type, string value = null, string name = null, string id = null, string cls = null)
            : base("input", id, cls)
        {
            tagValues["type"] = type;
            if (value != null)
                tagValues["value"] = value;
            if (name != null)
                tagValues["name"] = name;
        }

        public string OnClick
        {
            get
            {
                return tagValues.GetValueOrDefault("onclick", null);
            }
            set
            {
                tagValues["onclick"] = value;
            }
        }
    }
}
