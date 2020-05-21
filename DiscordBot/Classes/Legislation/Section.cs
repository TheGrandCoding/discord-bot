using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Classes.Legislation.Amending;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.Legislation
{
    public class Section
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
            Amendments = new List<SectionAmendment>();
            TextAmendments = new List<TextAmendment>();
        }
        [JsonProperty("no")]
        public string Number { get; set; }
        [JsonProperty("h")]
        public string Header { get; set; }
        [JsonProperty("c")]
        public List<Paragraph> Children { get; set; }
        [JsonProperty("ta")]
        public List<TextAmendment> TextAmendments { get; set; }
        [JsonProperty("a")]
        public List<SectionAmendment> Amendments { get; set; }
        [JsonProperty("g", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Group { get; set; } = false;

        string getAmendedText(AmendmentBuilder builder)
        {
            var amender = new TextAmenderBuilder(Header, builder, TextAmendments);
            return builder.TextOnly ? amender.NiceWords : amender.RawText;
        }

        public virtual void WriteTo(HTMLBase parent, int depth, AmendmentBuilder builder, ActAmendment amendAppliesThis)
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
            if (amendAppliesThis != null)
            {
                var next = builder.GetNextNumber(amendAppliesThis);
                if (amendAppliesThis.Type == AmendType.Repeal)
                {
                    RHS.RawText = builder.TextOnly ? "..." :  ". . . ." + LegHelpers.GetChangeAnchor(next);
                    return;
                }
                if (amendAppliesThis.Type == AmendType.Insert)
                {
                    LHS.RawText = (builder.TextOnly ? "" : $"{LegHelpers.GetChangeDeliminator(true)}{LegHelpers.GetChangeAnchor(next)}") + $"{Number}";
                }
            }
            var children = new List<Paragraph>();
            children.AddRange(Children);
            children.AddRange(Amendments.Where(x => x.Type == AmendType.Insert && x.NewParagraph != null).Select(x => x.NewParagraph));
            foreach(var child in children.OrderBy(x => x.Number, new NumberComparer()))
            {
                var amendmentApplies = Amendments.Where(x => x.Target == child.Number);
                var mostRelevant = amendmentApplies.FirstOrDefault(x => x.Type == AmendType.Repeal) ?? amendmentApplies.FirstOrDefault(x => x.Type == AmendType.Insert);

                child.WriteTo(parent, depth + 1, this, builder, mostRelevant);
            }
            if (amendAppliesThis?.Type == AmendType.Insert && !builder.TextOnly)
            {
                var last = parent.Children[^1];
                last.Children.Add(LegHelpers.GetChangeDeliminator(false));
            }
        }
    }
}
