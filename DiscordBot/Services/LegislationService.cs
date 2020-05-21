using DiscordBot.Classes;
using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Classes.Legislation;
using DiscordBot.Classes.Legislation.Amending;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DiscordBot.Services
{
    public class LegislationService : Service
    {
        public static string StorageFolder => Path.Combine(Program.BASE_PATH, "Legislation");

        public Dictionary<string, Act> Laws { get; set; }

        public void SaveAct(string pathName) => SaveAct(Laws[pathName]);

        public void SaveAct(Act act)
        {
            var content = JsonConvert.SerializeObject(act, new BotUserConverter());
            var path = Path.Combine(StorageFolder, $"{act.PathName}.json");
            File.WriteAllText(path, content);
        }

        public void RemoveAct(Act act) => RemoveAct(act.PathName);

        public void RemoveAct(string name)
        {
            Laws.Remove(name);
            var path = Path.Combine(StorageFolder, $"{name}.json");
            File.Delete(path);
        } 

        public HTMLPage PageForSection(Act act, Section section)
        {
            var div = new Div(cls: "LegSnippet");
            var amendmentApplies = act.Amendments.Where(x => x.Target == section.Number);
            var mostRelevant = amendmentApplies.FirstOrDefault(x => x.Type == AmendType.Repeal) ?? amendmentApplies.FirstOrDefault(x => x.Type == AmendType.Insert);
            var builder = new AmendmentBuilder(0, false);

            section.WriteTo(div, 1, builder, mostRelevant);
            if (builder.Performed.Count > 0)
            {
                div.Children.Add(builder.GetDiv());
            }
            var page = new HTMLPage()
            {
                Children =
                {
                    new PageHeader()
                        .WithStyle("https://www.legislation.gov.uk/styles/legislation.css")
                        .WithStyle("https://www.legislation.gov.uk/styles/primarylegislation.css")
                        .WithMeta(Meta.Charset("UTF-8"))
                        .WithOpenGraph(title: $"{act.ShortRef} Section {section.Number}", 
                                       description: section.Header),
                    new PageBody()
                    {
                        Children = { div }
                    }
                }
            };
            return page;
        }

        public HTMLPage PageForAct(Act act, bool printOnlyText)
        {
            var div = act.GetDiv(printOnlyText);
            var page = new HTMLPage()
            {
                Children =
                {
                    new PageHeader()
                        .WithStyle("https://www.legislation.gov.uk/styles/legislation.css")
                        .WithStyle("https://www.legislation.gov.uk/styles/primarylegislation.css")
                        .WithMeta(Meta.Charset("UTF-8"))
                        .WithOpenGraph(title: $"{act.ShortRef}",
                                       description: act.Title),
                    new PageBody()
                    {
                        Children = { div }
                    }
                }
            };
            return page;
        } 

        public override void OnSave()
        {
            foreach (var act in Laws.Values)
                SaveAct(act);
        }

        public override void OnLoaded()
        {
            if (!Directory.Exists(StorageFolder))
                Directory.CreateDirectory(StorageFolder);
            Laws = Laws ?? new Dictionary<string, Act>();
            var files = Directory.GetFiles(StorageFolder);
            foreach(var fileName in files)
            {
                var fileInfo = new FileInfo(fileName);
                if(fileInfo.Extension == ".json")
                {
                    var content = File.ReadAllText(fileInfo.FullName);
                    var act = JsonConvert.DeserializeObject<Act>(content, new BotUserConverter());
                    if(act.Draft == false && act.EnactedDate.HasValue == false)
                    {
                        act.EnactedDate = DateTime.Now;
                    }
                    Laws[act.PathName] = act;
                }
            }
        }
    }
}
