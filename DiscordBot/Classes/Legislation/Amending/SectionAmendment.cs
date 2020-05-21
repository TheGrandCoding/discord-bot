using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Legislation.Amending
{
    public class SectionAmendment : BaseAmendment
    {
        public string Target { get; set; }
        public Paragraph NewParagraph { get; set; }

        public override string GetDescription()
        {
            string s = $"through #{GroupId}, by {Group.Author.Name} on {Group.Date}";
            if (Type == AmendType.Insert)
                return $"Paragraph inserted {s}";
            if (Type == AmendType.Repeal)
                return $"Paragraph repealed {s}";
            return $"Paragraph replacement invalid amendment";
        }
    }
}
