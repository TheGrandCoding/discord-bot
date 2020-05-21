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
            if (Type == AmendType.Insert)
                return $"Paragraph inserted by {User} on {Date}";
            if (Type == AmendType.Repeal)
                return $"Paragraph repealed by {User} on {Date}";
            return $"Paragraph replacement invalid amendment";
        }
    }
}
