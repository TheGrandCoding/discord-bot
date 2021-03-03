using DiscordBot.Classes;
using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Classes.Legislation;
using DiscordBot.Classes.Legislation.Amending;
#if WINDOWS
using DiscordBot.Services.Acts;
#endif
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

        public string GetActPath(string pathName)
        {
            return Path.Combine(StorageFolder, pathName + ".json");
        }
        public string GetActPath(Act act)
        {
            return GetActPath(act.PathName);
        }

        public void SaveAct(string pathName) => SaveAct(Laws[pathName]);

        public void SaveAct(Act act)
        {
            var content = JsonConvert.SerializeObject(act, new BotUserConverter());
            var path = GetActPath(act.PathName);
            var fileInfo = new FileInfo(path);
            if (fileInfo.Directory.Exists == false)
                fileInfo.Directory.Create();
            File.WriteAllText(path, content);
        }

        public void RemoveAct(Act act) => RemoveAct(act.PathName);

        public void RemoveAct(string name)
        {
            Laws.Remove(name);
            var path = GetActPath(name);
            File.Delete(path);
        } 

        public HTMLPage PageForSection(Act act, Section section)
        {
            var div = new Div(cls: "LegSnippet");
            var builder = new AmendmentBuilder(0, false);

            section.WriteTo(div, 1, builder);
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
                        .WithOpenGraph(title: $"{act.ShortTitle} Section {section.Number}", 
                                       description: section.Header),
                    new PageBody()
                    {
                        Children = { div }
                    }
                }
            };
            return page;
        }

        public static HTMLPage PageForAct(Act act, bool printOnlyText)
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
                        .WithOpenGraph(title: $"{act.ShortTitle}",
                                       description: act.LongTitle)
                        .WithTitle(act.ShortTitle),
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
            if (Laws == null)
                return;
            foreach (var act in Laws.Values)
                SaveAct(act);
        }

        public override void OnLoaded()
        {
            if (!Directory.Exists(StorageFolder))
                Directory.CreateDirectory(StorageFolder);
            Laws = Laws ?? new Dictionary<string, Act>();
            var walk = Directory.GetFiles(StorageFolder, "*.json", SearchOption.AllDirectories);
            foreach(var fileName in walk)
            {
                var fileInfo = new FileInfo(fileName);
                var content = File.ReadAllText(fileInfo.FullName);
                var act = JsonConvert.DeserializeObject<Act>(content, new BotUserConverter());
                if(act.Draft == false && act.EnactedDate.HasValue == false)
                    act.EnactedDate = DateTime.Now;
                act.Register(null);
                Laws[act.PathName] = act;
            }
#if WINDOWS
            var a = ConstitutionAct.GetAct();
            Laws[a.PathName] = a;
#endif
        }
    }
}
