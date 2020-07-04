using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Legislation.Amending
{
    public class ThingAmendment : BaseAmendment
    {
        private string thing;
        
        public ThingAmendment(LawThing _thing, int id, AmendType type)
        {
            thing = _thing.GetType().Name;
            AmendsAct = _thing.Law;
            GroupId = id;
            Type = type;
        }

        public override string GetDescription()
        {
            var s = $"through #{GroupId}, by {Group.Author.Name} on {Group.Date}";
            if (Type == AmendType.Repeal)
                return $"{thing} repealed {s}";
            if (Type == AmendType.Insert)
                return thing + " inserted " + s;
            return thing + " replaced " + s;
        }
    }
}
