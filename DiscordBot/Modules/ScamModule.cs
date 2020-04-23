using Discord;
using Discord.Commands;
using DiscordBot.Commands;
using DiscordBot.Commands.Attributes;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
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
    [RequireOwner]
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
    
        [Command("add")]
        [Summary("Adds a new scam")]
        public async Task Add([Remainder]string name)
        {
            var scm = new Scam();
            scm.Name = name;
            await ReplyAsync("Your next message should provide the reason for why this is a scam");
            var rsn = await NextMessageAsync(timeout: TimeSpan.FromMinutes(5));
            if(rsn == null || string.IsNullOrWhiteSpace(rsn.Content))
            {
                await ReplyAsync("No message.");
                return;
            }
            scm.Reason = rsn.Content;

            var texts = new List<string>();
            while(true)
            {
                await ReplyAsync("Next message provides a trigger for this scam, or `q` to stop adding triggers");
                var msg = await NextMessageAsync(timeout: TimeSpan.FromMinutes(10));
                if(msg == null || string.IsNullOrWhiteSpace(msg.Content))
                {
                    await ReplyAsync("No message, cancelling entire operation.");
                    return;
                }
                if (msg.Content == "q")
                    break;
                texts.Add(msg.Content);
            }
            scm.Text = texts.ToArray();
            await ReplyAsync("Does this look right? Send a message within 15 seconds to confirm.", embed: embedForScam(scm));
            var conf = await NextMessageAsync(timeout: TimeSpan.FromSeconds(15));
            if(conf == null || string.IsNullOrWhiteSpace(conf.Content))
            {
                await ReplyAsync("Cancelled.");
                return;
            }
            Detector.Scams.Add(scm);
            Detector.OnSave();
        }

        [Command("list")]
        [Summary("Lists all scams registered")]
        public async Task List()
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "Scams";
            foreach(var scm in Detector.Scams)
            {
                builder.AddField(scm.Name, $"{scm.Text.Length} triggers;\r\n{scm.Reason}");
            }
            await ReplyAsync(embed: builder.Build());
        }

        Embed embedForScam(Scam scm)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = scm.Name;
            builder.Description = scm.Reason ?? "No reason provided.";
            foreach(var txt in scm.Text)
            {
                builder.Description += $"\r\n\r\n> {txt}";
            }
            return builder.Build();
        }
    }
}
