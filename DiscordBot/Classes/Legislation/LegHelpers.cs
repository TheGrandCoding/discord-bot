using DiscordBot.Classes.HTMLHelpers.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Legislation
{
    public static class LegHelpers
    {
        public static Span GetChangeDeliminator(bool openOrClose) => new Span(cls: "LegChangeDelimiter")
        {
            RawText = openOrClose ? "[" : "]"
        };

        public static Anchor GetChangeAnchor(string reference) => new Anchor($"#commentary-{reference}", id: $"reference-{reference}", cls: "LegCommentaryLink")
        {
            RawText = reference
        };
    }
}
