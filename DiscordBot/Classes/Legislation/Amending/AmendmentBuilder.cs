using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Legislation.Amending
{
    public class AmendmentBuilder
    {
        public List<BaseAmendment> Performed { get; set; }
        public bool TextOnly { get; set; }
        int startCount;
        public AmendmentBuilder(int start, bool printOnlyText)
        {
            TextOnly = printOnlyText;
            Performed = new List<BaseAmendment>();
            startCount = start;
        }

        public string GetNextNumber(BaseAmendment next)
        {
            if (next == null) return null;
            Performed.Add(next);
            return $"F{Performed.Count + startCount}";
        }

        public HTMLBase GetDiv()
        {
            if (TextOnly)
                return null;
            var div = new Div(cls: "LegAnnotations");
            div.Children.Add(new HTMLHelpers.Objects.Paragraph("Textual Amendments", cls: "LegAnnotationsGroupHeading"));
            for(int i = 0; i < Performed.Count; i++)
            {
                int n = i + startCount + 1; // zero offset
                var amend = Performed[i];
                var subDiv = new Div($"commentary-F{n}", "LegCommentaryItem")
                {
                    Children =
                    {
                        new HTMLHelpers.Objects.Paragraph(null, cls: "LegCommentaryPara")
                        {
                            Children =
                            {
                                new Span(cls: "LegCommentaryType")
                                {
                                    Children =
                                    {
                                        new Anchor($"#reference-F{n}", $"F{n}","Return to text")
                                    }
                                },
                                new Span(cls: "LegCommentaryText")
                                {
                                    RawText = amend.GetDescription()
                                }
                            }
                        }
                    }
                };
                div.Children.Add(subDiv);
            }
            return div;
        }
    }
}
