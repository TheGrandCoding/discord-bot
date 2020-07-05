using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.Legislation.Amending;
using Markdig;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Legislation
{
    public class Clause : LawThing
    {
        public Clause(string text)
        {
            Text = text;
            TextAmendments = new List<TextAmendment>();
        }
        [JsonProperty("tc")]
        public string Text { get; set; }

        [JsonProperty("a")]
        public List<TextAmendment> TextAmendments { get; set; }

        string getAmendedText(AmendmentBuilder builder)
        {
            string TEXT = Markdown.ToHtml(Text).Replace("<p>", "").Replace("</p>", "");

            var amender = new TextAmenderBuilder(TEXT, builder, TextAmendments);
            return builder.TextOnly ? amender.NiceWords : amender.RawText;
        }

        public void WriteTo(HTMLBase parent, int depth, AmendmentBuilder builder)
        {
            var paragraph = (Paragraph)Parent;
            var section = (Section)paragraph.Parent;
            var act = (Act)section.Parent;
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
            if (RepealedById.HasValue)
            {
                var next = builder.GetNextNumber(new ThingAmendment(this, RepealedById.Value, AmendType.Repeal));
                RHS.RawText = builder.TextOnly ? "..." : ". . . ." + LegHelpers.GetChangeAnchor(next);
                return;
            }
            else if (InsertedById.HasValue && InsertedById != Parent.InsertedById)
            {
                var next = builder.GetNextNumber(new ThingAmendment(this, InsertedById.Value, AmendType.Insert));
                LHS.RawText = (builder.TextOnly ? "" : $"{LegHelpers.GetChangeDeliminator(true)}{LegHelpers.GetChangeAnchor(next)}") + $"({Number})";
                RHS.RawText += builder.TextOnly ? "" : LegHelpers.GetChangeDeliminator(false);
            }
        }
    }
}
