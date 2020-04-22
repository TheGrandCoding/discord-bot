using Discord.Commands;
using DiscordBot.Commands;
using DiscordBot.Commands.Attributes;
using DiscordBot.Services;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    [Group("scam")]
    [Name("/r/DiscordApp Scam Checker")]
    [ReliesOnService(typeof(ScamDetector))]
    public class ScamModule : BotModule
    {
        public ScamDetector Detector { get; set; }

        async Task performTasks(string filename)
        {
            string temp = Path.Combine(Path.GetTempPath(), filename);
            /*Program.LogMsg("Now inverting B -> W");
            Bitmap pic = new Bitmap(temp);
            for (int y = 0; (y <= (pic.Height - 1)); y++)
            {
                for (int x = 0; (x <= (pic.Width - 1)); x++)
                {
                    Color inv = pic.GetPixel(x, y);
                    inv = Color.FromArgb(255, (255 - inv.R), (255 - inv.G), (255 - inv.B));
                    pic.SetPixel(x, y, inv);
                }
            }
            temp = Path.Combine(Path.GetTempPath(), $"inverted_{filename}");
            Program.LogMsg($"Inverted image to {temp}");
            pic.Save(temp);*/
            var results = Detector.getPossibleScams(temp, out var words);
            temp = Path.Combine(Path.GetTempPath(), $"words_{filename}.txt");
            File.WriteAllLines(temp, words);
            await ReplyAsync($"Found:\n" +
                string.Join("\n", results.Select(x => $"{x.Scam.Name}: {(x.Confidence * 100):00.0}%")));
            if (results.Select(x => x.Confidence).Any(x => x > 0.89))
            {
                await ReplyAsync($"That DM is a scam!");
            }
            else
            {
                await ReplyAsync("I don't think thats a scam");
            }
        }

        [Command("test")]
        [Summary("Tests an uploaded image for a scam")]
        public async Task<RuntimeResult> Test()
        {
            Program.LogMsg("Entered");
            var attachment = Context.Message.Attachments.FirstOrDefault();
            if (attachment == null)
                return new BotResult("You must upload an image, provide a URL, or previous filename as arg");
            await Test(new Uri(attachment.Url));
            return new BotResult();
        }

        [Command("test")]
        [Summary("Tests an image from the given Uri")]
        public async Task Test(Uri uri)
        {
            string path = uri.AbsolutePath;
            string file = path.Substring(path.LastIndexOf('/') + 1);
            var temp = Path.Combine(Path.GetTempPath(), file);
            Program.LogMsg("Downloading to " + temp);
            using (WebClient client = new WebClient())
            {
                client.DownloadFile(uri, temp);
            }
            await performTasks(file);
        }

        [Command("test")]
        [Summary("Tests a previously uploaded image for a scam")]
        public async Task Test(string name)
        {
            await performTasks(name);
        }
    }
}
