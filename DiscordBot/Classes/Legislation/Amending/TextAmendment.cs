using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.Legislation.Amending
{
    public class TextAmendment : BaseAmendment
    {
        public virtual int Start { get; set; }
        public int Length { get; set; }
        public string New { get; set; }

        public override string GetDescription()
        {
            return $"Words substitued by {User} on {Date}";
        }
    }
}
