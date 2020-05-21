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
            if (Type == AmendType.Insert)
                return $"Clause inserted by {User} on {Date}";
            if (Type == AmendType.Repeal)
                return $"Clause repealed by {User} on {Date}";
            return $"Clause replacement invalid amendment";
        }
    }
}
