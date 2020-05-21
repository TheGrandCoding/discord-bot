using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Legislation.Amending
{
    public class ParagraphAmendment : BaseAmendment
    {
        public string Target { get; set; }
        public Clause NewClause { get; set; }

        public override string GetDescription()
        {
            string s = $"through #{GroupId}, by {Group.Author.Name} on {Group.Date}";
            if (Type == AmendType.Insert)
                return $"Clause inserted {s}";
            if (Type == AmendType.Repeal)
                return $"Clause repealed {s}";
            return $"Clause replacement invalid amendment";
        }
    }
}
