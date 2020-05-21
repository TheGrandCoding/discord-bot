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
        [JsonConstructor]
        private Act(List<ActAmendment> a, List<Section> c)
        {
            Children = c;
            Amendments = a;
            foreach (var x in Amendments)
                x.AmendsAct = this;
            foreach (var x in Children)
            {
                foreach (var y in x.TextAmendments)
                    y.AmendsAct = this;
                foreach (var y in x.Amendments)
                    y.AmendsAct = this;
                foreach(var p in x.Children)
                {
                    foreach (var y in p.Amendments)
                        y.AmendsAct = this;
                    foreach (var y in p.TextAmendments)
                        y.AmendsAct = this;
                    foreach (var cl in p.Children)
                        foreach (var y in cl.TextAmendments)
                            y.AmendsAct = this;
                }
            }
            AmendmentReferences = new Dictionary<int, AmendmentGroup>();
        }

        public Act(string title)
        {
            Title = title;
            Children = new List<Section>();
            Amendments = new List<ActAmendment>();
            AmendmentReferences = new Dictionary<int, AmendmentGroup>();
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

        [JsonIgnore]
        public string URL => $"{MLAPI.Handler.LocalAPIUrl}/laws/{PathName}";

        [JsonProperty("d", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Draft { get; set; } = false;
        [JsonProperty("ed", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? EnactedDate { get; set; }

        [JsonProperty("amr")]
        public Dictionary<int, AmendmentGroup> AmendmentReferences { get; set; }

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

        public HTMLBase GetDiv(bool printTextOnly)
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
                var builder = new AmendmentBuilder(amendmentCount, printTextOnly);

                child.WriteTo(div, 1, builder, mostRelevant);
                if(builder.Performed.Count > 0)
                {
                    amendmentCount += builder.Performed.Count;
                    if(!builder.TextOnly)
                        div.Children.Add(builder.GetDiv());
                }
            }
            return div;
        }
    }
}
