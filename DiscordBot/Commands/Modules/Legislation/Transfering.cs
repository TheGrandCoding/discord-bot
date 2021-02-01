using Discord.Commands;
using DiscordBot.Classes.Legislation;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules.Legislation
{
    [Group("law")]
    [RequireOwner]
    [Name("Legislation ExportImport")]
    public class Transfering : BotBase
    {
        public LegislationService Service { get; set; }

        [Command("export")]
        public async Task Export(string name)
        {
            if(!Service.Laws.TryGetValue(name, out var law))
            {
                await ReplyAsync("No such law exists; available: \r\n" + string.Join("\r\n", Service.Laws.Keys));
                return;
            }
            Service.SaveAct(law);
            var lawPath = Service.GetActPath(law);
            await Context.Channel.SendFileAsync(lawPath);
        }

        [Command("remove")]
        public async Task Remove(string name)
        {
            if (!Service.Laws.TryGetValue(name, out var law))
            {
                await ReplyAsync("No such law exists; available: \r\n" + string.Join("\r\n", Service.Laws.Keys));
                return;
            }
            await Export(name);
            Service.RemoveAct(name);
        }



        [Command("import")]
        public async Task Import()
        {
            var attch = Context.Message.Attachments.First();
            using var wc = new WebClient();
            var file = Path.GetTempFileName();
            wc.DownloadFile(attch.Url, file);
            var content = File.ReadAllText(file);
            var act = Program.Deserialise<Act>(content);
            Service.Laws[act.PathName] = act;
            Service.OnSave();
        }
    }
}
