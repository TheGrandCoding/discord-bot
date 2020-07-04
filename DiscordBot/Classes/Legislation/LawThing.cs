using DiscordBot.Classes.Legislation.Amending;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Legislation
{
    public class LawThing
    {
        [JsonProperty("no")]
        public string Number { get; set; }
        [JsonProperty("in")]
        public int? InsertedById { get; set; }
        [JsonProperty("re")]
        public int? RepealedById { get; set; }

        [JsonIgnore]
        public ThingAmendment InsertedBy {  get
            {
                if(!InsertedById.HasValue)
                    return null;
                return new ThingAmendment(this, InsertedById.Value, AmendType.Insert);
            } }
        [JsonIgnore]
        public ThingAmendment RepealedBy
        {
            get
            {
                if (!RepealedById.HasValue)
                    return null;
                return new ThingAmendment(this, RepealedById.Value, AmendType.Repeal);
            }
        }

        [JsonIgnore]
        public virtual Act Law => Parent?.Law;
        [JsonIgnore]
        public LawThing Parent { get; set; }

        public virtual void Register(LawThing parent)
        {
            Parent = parent;
        }
    }
    public class LawThing<TChild> : LawThing where TChild : LawThing
    {
        [JsonProperty("c", ItemTypeNameHandling = TypeNameHandling.All)]
        public List<TChild> Children { get; set; }

        public override void Register(LawThing parent)
        {
            base.Register(parent);
            foreach (var child in Children)
                child.Register(this);
        }
    }
}
