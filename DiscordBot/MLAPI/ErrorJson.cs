using DiscordBot.Classes.Converters;
using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
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
            var table = new Table()
            {
                Children =
                {
                    new TableRow()
                    .WithHeader("Endpoint")
                    .WithHeader("Reason(s)")
                }
            };
            foreach(var error in errors)
            {
                var row = new TableRow();
                if(error.endpoint == null)
                {
                    row.WithCell($"*");
                } else
                {
                    row.WithCell(error.endpoint.GetDocs());
                }
                row.WithCell(error.reason);
                table.Children.Add(row);
            }
            div.Children.Add(table);
            return div;
        }

        public HTMLPage GetPrettyPage(APIContext context)
        {
            var page = new HTMLPage()
            {
                Children =
                {
                    new PageHeader()
                        .WithStyle("docs.css")
                }
            };
            var body = new PageBody();
            body.Children.Add(browser(context));
            page.Children.Add(body);
            return page;
        }

        public string GetSimpleText() => string.Join("; ", errors.Select(x => x.reason));
    }

    [JsonConverter(typeof(ErrorJsonConverter))]
    public class ErrorItem
    {
        public APIEndpoint endpoint { get; set; }
        public string reason { get; set; }
        public ErrorItem(APIEndpoint e, string r)
        {
            endpoint = e;
            reason = r;
        }
    }
}
