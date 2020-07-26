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
            var s = $"through #{GroupId}, by {Group.Author.Name} on {Group.Date}";
            if (Type == AmendType.Insert)
                return $"Words inserted {s}";
            if(Type == AmendType.Repeal)
                return $"Words removed {s}";
            if(Type == AmendType.Substitute)
                return $"Words substitued {s}";
            return "Unknown action";
        }
    }
}
