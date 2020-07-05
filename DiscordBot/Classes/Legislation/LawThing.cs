using DiscordBot.Classes.Legislation.Amending;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        public void Sort()
        {
            Children.Sort(new ThingComparer());
        }

        private class ThingComparer : IComparer<LawThing>
        {
            int IComparer<LawThing>.Compare(LawThing x, LawThing y)
            {
                return (int)Compare(x, y);
            }
            enum Comparison
            {
                Behind = -1,
                Equal = 0,
                Ahead = 1,
            }

            Comparison Compare(LawThing x, LawThing y)
            {
                var xLetters = x.Number.Split();
                var yLetters = y.Number.Split();
                for(int index = 0; index < xLetters.Length && index < yLetters.Length; index++)
                {
                    var xLet = xLetters[index];
                    var yLet = yLetters[index];
                    var compare = (Comparison)xLet.CompareTo(yLet);
                    if (compare != Comparison.Equal)
                        return compare;
                }
                if(x is Section s)
                {
                    if (s.Group)
                        return Comparison.Behind;
                }
                if(y is Section ys)
                {
                    if (ys.Group)
                        return Comparison.Ahead;
                }
                return Comparison.Equal;
            }
        }
    }

}
