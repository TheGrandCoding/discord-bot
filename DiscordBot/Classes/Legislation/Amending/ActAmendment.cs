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
            if (Type == AmendType.Insert)
                return $"Section inserted by {User} on {Date}";
            if (Type == AmendType.Repeal)
                return $"Section repealed by {User} on {Date}";
            return $"Section replacement invalid amendment";
        }
    }
}
