using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{
    [RequireApproval(false)]
    [RequireAuthentication(false)]
    [RequireVerifiedAccount(false)]
    public class Legal : APIBase
    {
        public Legal(APIContext context) : base(context, "legal") 
        {
        }

        [Method("GET"), Path("/terms")]
        public async Task Terms()
        {
            ReplyFile("terms.html", 200);
        }

        [Method("GET"), Path("/privacy")]
        public async Task Privacy()
        {
            ReplyFile("privacy.html", 200);
        }

        HTMLBase getDataServices()
        {
            var div = new Div();
            div.Children.Add(new H2().WithRawText("Services"));
            foreach(var service in Service.GetServices<ISARProvider>())
            {
                var sv = (Service)service;
                var para = new Paragraph(null);
                para.Children.Add(new H3().WithRawText(sv.Name));
                var jobj = service.GetSARDataFor(Context.User.Id);
                string data;
                if (jobj == null)
                    data = "*No data stored on you*";
                else
                    data = jobj.ToString(Newtonsoft.Json.Formatting.Indented);
                para.Children.Add(new RawObject("<pre>" + 
                    data
                    + "</pre>"));
                div.Children.Add(para);
            }
            return div;
        }

        [Method("GET"), Path("/privacy/sar")]
        [RequireAuthentication(true)]
        public async Task SAR()
        {
            var accountData = Program.Serialise(Context.User, format: Newtonsoft.Json.Formatting.Indented);
            var serviceData = getDataServices();
            ReplyFile("sar.html", 200, new Replacements()
                .Add("account", accountData)
                .Add("services", serviceData));
        }
    }
}
