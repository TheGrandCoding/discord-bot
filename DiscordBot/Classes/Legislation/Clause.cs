using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.Legislation.Amending;
using Markdig;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Legislation
{
    public class Clause
    {
        public Clause(string text)
        {
            Text = text;
            Amendments = new List<TextAmendment>();
        }
        [JsonProperty("tc")]
        public string Text { get; set; }
        [JsonProperty("no")]
        public string Number { get; set; }

        [JsonProperty("a")]
        public List<TextAmendment> Amendments { get; set; }

        string getAmendedText(AmendmentBuilder builder)
        {
            string TEXT = Markdown.ToHtml(Text).Replace("<p>", "").Replace("</p>", "");

            var amender = new TextAmenderBuilder(TEXT, builder, Amendments);
            return amender.RawText;
        }

        public void WriteTo(HTMLBase parent, int depth, Section section, Paragraph paragraph, AmendmentBuilder builder, ParagraphAmendment amendAppliesThis)
        {
            var LHS = new HTMLHelpers.Objects.Span($"section-{section.Number}-{paragraph.Number}-{Number}", $"LegDS LegLHS LegP{depth}No") { RawText = $"({Number})" };
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
            if (amendAppliesThis != null)
            {
                var next = builder.GetNextNumber(amendAppliesThis);
                if (amendAppliesThis.Type == AmendType.Repeal)
                {
                    RHS.RawText = ". . . ." + LegHelpers.GetChangeAnchor(next);
                    return;
                }
                if (amendAppliesThis.Type == AmendType.Insert)
                {
                    LHS.RawText = $"{LegHelpers.GetChangeDeliminator(true)}{LegHelpers.GetChangeAnchor(next)}({Number})";
                    RHS.RawText += LegHelpers.GetChangeDeliminator(false);
                }
            }
        }
    }
}
