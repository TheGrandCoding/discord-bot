using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Classes.Legislation.Amending;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.Legislation
{
    public class Section : LawThing<Paragraph>
    {
        public static Section NewGroup(string name)
        {
            return new Section(name)
            {
                Group = true
            };
        }
        public Section(string header)
        {
            Header = header;
            Children = new List<Paragraph>();
            TextAmendments = new List<TextAmendment>();
        }
        [JsonProperty("h")]
        public string Header { get; set; }
        [JsonProperty("ta")]
        public List<TextAmendment> TextAmendments { get; set; }
        [JsonProperty("g", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Group { get; set; } = false;

        string getAmendedText(AmendmentBuilder builder)
        {
            var amender = new TextAmenderBuilder(Header, builder, TextAmendments);
            return builder.TextOnly ? amender.NiceWords : amender.RawText;
        }

        public virtual void WriteTo(HTMLBase parent, int depth, AmendmentBuilder builder)
        {
            if(Group)
            {
                parent.Children.Add(new Div(cls: "LegClearPblock"));
                parent.Children.Add(new H2(cls: "LegPblock")
                {
                    Children =
                    {
                        new Span(cls: "LegPblockTitle") {RawText = getAmendedText(builder)}
                    }
                });
                return;
            }
            var LHS = new Span(cls: $"LegDS LegP{depth}No") { RawText = Number };
            var RHS = new Span(cls: $"LegDS LegP{depth}GroupTitle") { RawText = getAmendedText(builder) };
            var header = new H3($"section-{Number}", $"LegClearFix LegP{depth}Container{(Number == "1" ? "First" : "")}")
            {
                Children =
                {
                    LHS, RHS
                }
            };
            parent.Children.Add(header);
            if(RepealedById.HasValue)
            {
                var amend = Law.AmendmentReferences[RepealedById.Value];
                var next = builder.GetNextNumber(new ThingAmendment(this, RepealedById.Value, AmendType.Repeal));
                RHS.RawText = builder.TextOnly ? "..." :  ". . . ." + LegHelpers.GetChangeAnchor(next);
                return;
            } else if (InsertedById.HasValue)
            {
                var amend = Law.AmendmentReferences[InsertedById.Value];
                var next = builder.GetNextNumber(new ThingAmendment(this, RepealedById.Value, AmendType.Insert));
                LHS.RawText = (builder.TextOnly ? "" : $"{LegHelpers.GetChangeDeliminator(true)}{LegHelpers.GetChangeAnchor(next)}") + $"{Number}";
            }
            var children = new List<Paragraph>();
            children.AddRange(Children);
            foreach(var child in children.OrderBy(x => x.Number, new NumberComparer()))
            {
                child.WriteTo(parent, depth + 1, builder);
            }
            if (InsertedById.HasValue && !builder.TextOnly)
            {
                var last = parent.Children[^1];
                last.Children.Add(LegHelpers.GetChangeDeliminator(false));
            }
        }
    }
}
