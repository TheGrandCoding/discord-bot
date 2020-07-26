using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.Legislation.Amending;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.Legislation
{
    public abstract class LawThing
    {
        [JsonProperty("no")]
        public string Number { get; set; }
        [JsonProperty("in", NullValueHandling = NullValueHandling.Ignore)]
        public int? InsertedById { get; set; }
        [JsonProperty("re", NullValueHandling = NullValueHandling.Ignore)]
        public int? RepealedById { get; set; }

        /// <summary>
        /// This should be set on the new substituted element so it can properly indicate it is substituted
        /// </summary>
        [JsonProperty("sn", NullValueHandling = NullValueHandling.Ignore)]
        public int? SubstituedById { get; set; }

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

        [JsonProperty("sb", NullValueHandling = NullValueHandling.Ignore)]
        public ThingSubstitution Substituted { get; set; }

        [JsonIgnore]
        public virtual Act Law => Parent?.Law;
        [JsonIgnore]
        public LawThing Parent { get; set; }

        [JsonIgnore]
        public string HierarchyId { get
            {
                if (Parent == null)
                    return "";
                var s = Parent.HierarchyId;
                if (s.Length > 0)
                    return s + "-" + Number;
                return Number;
            } }

        public virtual void Register(LawThing parent)
        {
            Parent = parent;
        }

        public abstract void WriteTo(HTMLHelpers.HTMLBase parent, int depth, AmendmentBuilder builder);

        public virtual void SetInitialNumber(int depth, int count)
        {
            if (Substituted != null)
                Substituted.New.SetInitialNumber(depth, count);
            if (Number != null)
                return;
            if (depth == 0 || depth == 1)
                Number = count.ToString();
            else if (depth == 2)
                Number = Convert.ToChar(96 + count).ToString();
            else if (depth == 3)
                Number = getRomanNumerals(count);
        }

        string getRomanNumerals(int value)
        {
            string s = "";
            int tens = value / 10;
            value -= (tens * 10);
            if (tens > 0)
                s += new string('x', tens);
            int fives = value / 5;
            value -= (fives * 5);
            if (fives > 0)
                s += new string('v', fives);
            if(value <= 3)
                s += new string('i', value);
            else if (value == 4)
                s += "iv";
            return s;
        }
    
        protected virtual string getLine(int depth, int count) => $"{new string(' ', depth * 4)}{count:00} {Number}";
    }
    public abstract class LawThing<TChild> : LawThing where TChild : LawThing
    {
        [JsonProperty("c", ItemTypeNameHandling = TypeNameHandling.All)]
        public List<TChild> Children { get; set; } = new List<TChild>();

        public override void Register(LawThing parent)
        {
            base.Register(parent);
            foreach (var child in Children)
                child.Register(this);
        }

        public virtual LawThing Find(params string[] things)
        {
            if (things.Length == 0)
                return null;
            dynamic child = Children.FirstOrDefault(x => x.Number == things[0]);
            if (things.Length == 1)
                return child;
            return child?.Find(things[1..]);
        }
        public LawThing Find(string thing) => Find(thing.Split('-', ' ', ',', '.'));

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

        public override void SetInitialNumber(int depth, int count)
        {
            base.SetInitialNumber(depth, count);
            count = 1;
            Children = Children.OrderBy(x => x.Number, new NumberComparer()).ToList();
            foreach (var child in Children)
                child.SetInitialNumber(depth + 1, count++);
        }
    }

    public class TextualLawThing : LawThing<TextualLawThing>
    {
        public TextualLawThing()
        {
        }

        public TextualLawThing(string text)
        {
            if (text.EndsWith("--"))
                text = text.Substring(0, text.Length - 2) + "—";
            Text = text;
        }

        [JsonProperty("t")]
        public string Text { get; set; }
        [JsonProperty("a")]
        public List<TextAmendment> TextAmendments { get; set; } = new List<TextAmendment>();

        public override void WriteTo(HTMLBase parent, int depth, AmendmentBuilder builder)
        {
            var act = this.Law;
            var LHS = new HTMLHelpers.Objects.Span(HierarchyId, $"LegDS LegLHS LegP{depth}No")
            {
                RawText = $"({Number})"
            };
            var RHS = new HTMLHelpers.Objects.Span(cls: $"LegDS LegRHS LegP{depth}Text")
            {
                RawText = getAmendedText(builder)
            };
            var p = new HTMLHelpers.Objects.Paragraph(null, null, $"LegClearFix LegP{depth}Container")
            {
                Children =
                {
                    LHS, RHS
                }
            };
            parent.Children.Add(p);
            if (RepealedById.HasValue)
            {
                var next = builder.GetNextNumber(new ThingAmendment(this, RepealedById.Value, AmendType.Repeal));
                RHS.RawText = builder.TextOnly ? "..." : ". . . ." + LegHelpers.GetChangeAnchor(next);
                return;
            } else if(Substituted != null)
            {
                parent.Children.RemoveAt(parent.Children.Count - 1); // remove p, since we'll refer to the substituted
                Substituted.New.Register(Parent);
                Substituted.New.Number = Number;
                Substituted.New.SubstituedById = Substituted.GroupId;
                Substituted.New.WriteTo(parent, depth, builder);
                return;
            } else if (SubstituedById.HasValue && Parent.SubstituedById != SubstituedById)
            {
                var next = builder.GetNextNumber(new ThingAmendment(this, SubstituedById.Value, AmendType.Substitute));
                LHS.RawText = (builder.TextOnly ? "" : $"{LegHelpers.GetChangeDeliminator(true)}{LegHelpers.GetChangeAnchor(next)}") + $"({Number})";
            }
            else if (InsertedById.HasValue && Parent.InsertedById != InsertedById)
            {
                var next = builder.GetNextNumber(new ThingAmendment(this, InsertedById.Value, AmendType.Insert));
                LHS.RawText = (builder.TextOnly ? "" : $"{LegHelpers.GetChangeDeliminator(true)}{LegHelpers.GetChangeAnchor(next)}") + $"({Number})";
            }
            foreach (var child in Children)
            {
                child.WriteTo(parent, depth + 1, builder);
            }
            if (!builder.TextOnly && (
                    (InsertedById.HasValue && Parent.InsertedById != InsertedById)
                    ||
                    (SubstituedById.HasValue && Parent.SubstituedById != SubstituedById)
                )
            )
            {
                var last = parent.Children[^1];
                var text = last.Children[1];
                text.Children.Add(LegHelpers.GetChangeDeliminator(false));
            }
        }

        protected string getAmendedText(AmendmentBuilder builder)
        {
            string TEXT = Markdig.Markdown.ToHtml(Text).Replace("<p>", "").Replace("</p>", "");
            var amender = new TextAmenderBuilder(TEXT, builder, TextAmendments);
            return builder.TextOnly ? amender.NiceWords : amender.RawText;
        }

        public static implicit operator TextualLawThing(string val)
        {
            return new LawText(val);
        }

        [JsonConverter(typeof(LawText.Converter))]
        public class LawText : TextualLawThing
        {
            public LawText(string t)
            {
                Text = t;
            }

            public override void WriteTo(HTMLBase parent, int depth, AmendmentBuilder builder)
            {
                depth--;
                var LHS = new HTMLHelpers.Objects.Span($"{Parent.HierarchyId}", $"LegDS LegLHS LegP{depth}No") { RawText = $"" };
                var RHS = new HTMLHelpers.Objects.Span(cls: $"LegDS LegRHS LegP{depth}Text")
                {
                    RawText = getAmendedText(builder)
                };

                var p = new HTMLHelpers.Objects.Paragraph(null, null, $"LegClearFix LegP{depth}Container")
                {
                    Children =
                    {
                        LHS, RHS
                    }
                };
                if (RepealedById.HasValue)
                {
                    var next = builder.GetNextNumber(new ThingAmendment(this, RepealedById.Value, AmendType.Repeal));
                    RHS.RawText = builder.TextOnly ? "..." : ". . . ." + LegHelpers.GetChangeAnchor(next);
                } else if (Substituted != null)
                {
                    Substituted.New.Register(Parent);
                    Substituted.New.WriteTo(parent, depth, builder);
                    return;
                }
                else if (InsertedById.HasValue && InsertedById != Parent.InsertedById)
                {
                    var next = builder.GetNextNumber(new ThingAmendment(this, InsertedById.Value, AmendType.Insert));
                    LHS.RawText = (builder.TextOnly ? "" : $"{LegHelpers.GetChangeDeliminator(true)}{LegHelpers.GetChangeAnchor(next)}") + $"({Number})";
                    RHS.RawText += builder.TextOnly ? "" : LegHelpers.GetChangeDeliminator(false);
                }
                parent.Children.Add(p);
            }

            public class Converter : JsonConverter
            {
                public override bool CanConvert(Type objectType)
                {
                    return objectType == typeof(LawText);
                }

                public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
                {
                    return new LawText((string)reader.Value);
                }

                public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                {
                    string str = null;
                    if (value is LawText lt)
                        str = lt.Text;
                    var jval = str == null ? JValue.CreateNull() : new JValue(str);
                    jval.WriteTo(writer);
                }
            }
        }
    }
}
