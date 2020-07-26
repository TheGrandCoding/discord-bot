using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Legislation.Amending
{
    public class ThingAmendment : BaseAmendment
    {
        public ThingAmendment(LawThing _thing, int id, AmendType type)
        {
            AmendsAct = _thing?.Law;
            GroupId = id;
            Type = type;
        }

        public override string GetDescription()
        {
            var s = $"through #{GroupId}, by {Group.Author.Name} on {Group.Date}";
            var action = Type.ToString().ToLower();
            if (action.EndsWith('e'))
                action = action.Substring(0, action.Length - 1);
            return $"Item {action}ed {s}";
        }
    }

    public class ThingSubstitution : ThingAmendment
    {
        public ThingSubstitution(LawThing _thing, int id) : base(_thing, id, AmendType.Substitute)
        {
            New = _thing;
        }

        [JsonProperty(TypeNameHandling = TypeNameHandling.All)]
        public LawThing New { get; set; }
    }
}
