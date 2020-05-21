using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.Legislation.Amending;
using Markdig;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.Legislation
{
    public class Paragraph
    {
        public Paragraph(string text)
        {
            Text = text;
            Children = new List<Clause>();
            TextAmendments = new List<TextAmendment>();
            Amendments = new List<ParagraphAmendment>();
        }
        [JsonProperty("tp")]
        public string Text { get; set; }
        [JsonProperty("no")]
        public string Number { get; set; }

        [JsonProperty("ta")]
        public List<TextAmendment> TextAmendments { get; set; }
        [JsonProperty("am")]
        public List<ParagraphAmendment> Amendments { get; set; }

        public List<Clause> Children { get; set; }

        string getAmendedText(AmendmentBuilder builder)
        {
            string TEXT = Markdown.ToHtml(Text).Replace("<p>", "").Replace("</p>", "");

            var amender = new TextAmenderBuilder(TEXT, builder, TextAmendments);
            return builder.TextOnly ? amender.NiceWords : amender.RawText;
        }

        public void WriteTo(HTMLBase parent, int depth, Section section, AmendmentBuilder builder, SectionAmendment amendAppliesThis)
        {
            var LHS = new HTMLHelpers.Objects.Span($"section-{section.Number}-{Number}", $"LegDS LegLHS LegP{depth}No") { RawText = $"({Number})" };
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
            if(amendAppliesThis != null)
            {
                var next = builder.GetNextNumber(amendAppliesThis);
                if (amendAppliesThis.Type == AmendType.Repeal)
                {
                    RHS.RawText = builder.TextOnly ? "..." : ". . . ." + LegHelpers.GetChangeAnchor(next);
                    return;
                }
                if (amendAppliesThis.Type == AmendType.Insert)
                {
                    LHS.RawText = (builder.TextOnly ? "" : $"{LegHelpers.GetChangeDeliminator(true)}{LegHelpers.GetChangeAnchor(next)}") + $"{Number}";
                }
            }
            foreach (var child in Children)
            {
                var amendmentApplies = Amendments.Where(x => x.Target == child.Number);
                var mostRelevant = amendmentApplies.FirstOrDefault(x => x.Type == AmendType.Repeal) ?? amendmentApplies.FirstOrDefault(x => x.Type == AmendType.Insert);

                child.WriteTo(parent, depth + 1, section, this, builder, mostRelevant);
            }
            if(amendAppliesThis?.Type == AmendType.Insert && !builder.TextOnly)
            {
                var last = parent.Children[^1];
                var text = last.Children[1];
                text.Children.Add(LegHelpers.GetChangeDeliminator(false));
            }
        }
    }

}
