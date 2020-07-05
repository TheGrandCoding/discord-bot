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
    public class Paragraph : LawThing<Clause>
    {
        public Paragraph(string text)
        {
            Text = text;
            Children = new List<Clause>();
            TextAmendments = new List<TextAmendment>();
        }
        [JsonProperty("tp")]
        public string Text { get; set; }

        [JsonProperty("ta")]
        public List<TextAmendment> TextAmendments { get; set; }

        string getAmendedText(AmendmentBuilder builder)
        {
            string TEXT = Markdown.ToHtml(Text).Replace("<p>", "").Replace("</p>", "");

            var amender = new TextAmenderBuilder(TEXT, builder, TextAmendments);
            return builder.TextOnly ? amender.NiceWords : amender.RawText;
        }

        public void WriteTo(HTMLBase parent, int depth, AmendmentBuilder builder)
        {
            var act = this.Law;
            var section = (Section)this.Parent;
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
            if(RepealedById.HasValue)
            {
                var next = builder.GetNextNumber(new ThingAmendment(this, RepealedById.Value, AmendType.Repeal));
                RHS.RawText = builder.TextOnly ? "..." : ". . . ." + LegHelpers.GetChangeAnchor(next);
                return;
            } else if (InsertedById.HasValue && Parent.InsertedById != InsertedById)
            {
                var next = builder.GetNextNumber(new ThingAmendment(this, InsertedById.Value, AmendType.Insert));
                LHS.RawText = (builder.TextOnly ? "" : $"{LegHelpers.GetChangeDeliminator(true)}{LegHelpers.GetChangeAnchor(next)}") + $"{Number}";
            }
            foreach (var child in Children)
            {
                child.WriteTo(parent, depth + 1, builder);
            }
            if(InsertedById.HasValue && !builder.TextOnly && Parent.InsertedById != InsertedById)
            {
                var last = parent.Children[^1];
                var text = last.Children[1];
                text.Children.Add(LegHelpers.GetChangeDeliminator(false));
            }
        }
    }

}
