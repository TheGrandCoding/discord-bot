using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Legislation.Amending
{
    public class ActAmendment : BaseAmendment
    {
        public string Target { get; set; }
        public Section NewSection { get; set; }

        public override string GetDescription()
        {
            string s = $"through #{GroupId}, by {Group.Author.Name} on {Group.Date}";
            if (Type == AmendType.Insert)
                return $"Section inserted {s}";
            if (Type == AmendType.Repeal)
                return $"Section repealed {s}";
            return $"Section replacement invalid amendment";
        }
    }
}
