using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class ErrorJson
    {
        public List<ErrorItem> errors { get; set; }

        public ErrorJson(List<ErrorItem> e)
        {
            errors = e;
        }

        public ErrorJson(string reason) : this(new List<ErrorItem>() { new ErrorItem(null, reason) })
        {
        }

        HTMLBase browser(APIContext context)
        {
            var div = new Div();
            div.Children.Add(new Paragraph("One or more errors have occured that have prevented this page from being loaded"));
            var ul = new UnorderedList();
            foreach(var errorItem in errors)
            {
                var li = new ListItem(null)
                {
                    Children =
                    {
                        new StrongText(errorItem.path),
                        new EmphasisText(" " + errorItem.reason)
                    }
                };
                ul.Children.Add(li);
            }
            div.Children.Add(ul);
            return div;
        }

        public HTMLPage GetPrettyPage(APIContext context)
        {
            var page = new HTMLPage()
            {
                Children =
                {
                    new PageHeader()
                }
            };
            var body = new PageBody();
            body.Children.Add(browser(context));
            page.Children.Add(body);
            return page;
        }
    }
    public class ErrorItem
    {
        public string path { get; set; }
        public string reason { get; set; }
        public ErrorItem(string p, string r)
        {
            path = p; reason = r;
        }
    }
}
