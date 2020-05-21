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
    public class Act
    {
        public Act(string title)
        {
            Title = title;
            Children = new List<Section>();
            Amendments = new List<ActAmendment>();
        }
        [JsonProperty("t")]
        public string Title { get; set; }
        [JsonProperty("pn")]
        public string PathName { get; set; }
        [JsonProperty("sr")]
        public string ShortRef { get; set; }
        [JsonProperty("c")]
        public List<Section> Children { get; set; }
        [JsonProperty("a")]
        public List<ActAmendment> Amendments { get; set; }

        [JsonProperty("d", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Draft { get; set; } = false;
        [JsonProperty("ed", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? EnactedDate { get; set; }

        HTMLBase GetPrelimBlock()
        {
            var div = new Div(cls: "LegClearFix LegPrelims")
            {
                Children =
                {
                    new H1(cls: "LegTitle") {RawText = ShortRef},
                    new H1(cls: "LegNo") {RawText = PathName},
                    new HTMLHelpers.Objects.Paragraph(Title, cls: "LegLongTitle"),
                    new HTMLHelpers.Objects.Paragraph(
                        EnactedDate.HasValue
                        ? $"[{string.Format("{0:dddd dd}{1} {0:MMMM yyyy}", EnactedDate, Program.GetDaySuffix(EnactedDate.Value.Day))}]"
                        : $"[DRAFT]",
                        cls: "LegDateOfEnactment"
                        )
                }
            };
            return div;
        }

        public HTMLBase GetDiv()
        {
            var div = new Div(cls: "LegSnippet");
            div.Children.Add(GetPrelimBlock());
            var children = new List<Section>();
            children.AddRange(Children);
            children.AddRange(Amendments.Where(x => x.Type == AmendType.Insert && x.NewSection != null).Select(x => x.NewSection));
            int count = 1;
            int amendmentCount = 0;
            foreach(var child in children.OrderBy(x => x.Number, new NumberComparer()))
            {
                if(child.Number == null)
                {
                    child.Number = $"{count}";
                    if (!child.Group)
                        count++;
                }
                // Amendments applied at the Act-level should either be:
                // a) Appeal an entire section
                // b) Insert an entire section
                // As such, there can only be one of each.
                // It's possible that an inserted section is repealed - but we'll handle repeals first anyway.
                var amendmentApplies = Amendments.Where(x => x.Target == child.Number);
                ActAmendment mostRelevant = amendmentApplies.FirstOrDefault(x => x.Type == AmendType.Repeal) ?? amendmentApplies.FirstOrDefault(x => x.Type == AmendType.Insert);
                var builder = new AmendmentBuilder(amendmentCount);

                child.WriteTo(div, 1, builder, mostRelevant);
                if(builder.Performed.Count > 0)
                {
                    amendmentCount += builder.Performed.Count;
                    div.Children.Add(builder.GetDiv());
                }
            }
            return div;
        }
    }
}
