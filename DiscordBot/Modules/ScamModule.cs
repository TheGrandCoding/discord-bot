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

        [Command("inverse")]
        [Summary("Tests the inverse of the image for a scam")]
        public async Task CheckInverse(Uri uri)
        {
            string path = uri.AbsolutePath;
            string file = path.Substring(path.LastIndexOf('/') + 1);
            var temp = Path.Combine(Path.GetTempPath(), file);
            Program.LogMsg("Downloading to " + temp);
            using (WebClient client = new WebClient())
            {
                client.DownloadFile(uri, temp);
            }
            Program.LogMsg("Now inverting image");
            Bitmap pic = new Bitmap(temp);
            for (int y = 0; (y <= (pic.Height - 1)); y++)
            {
                for (int x = 0; (x <= (pic.Width - 1)); x++)
                {
                    System.Drawing.Color inv = pic.GetPixel(x, y);
                    inv = System.Drawing.Color.FromArgb(255, (255 - inv.R), (255 - inv.G), (255 - inv.B));
                    pic.SetPixel(x, y, inv);
                }
            }
            file = $"inverted_{file}";
            temp = Path.Combine(Path.GetTempPath(), file);
            Program.LogMsg($"Inverted image to {temp}");
            pic.Save(temp);
            await performTasks(file);
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
                texts.Add(msg.Content.ToLower());
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
            await ReplyAsync("Added!");
        }

        [Command("modify")]
        [Summary("Update or remove existing triggers")]
        public async Task<RuntimeResult> AddNewTrigger([Remainder]string name)
        {
            var scm = Detector.Scams.FirstOrDefault(x => x.Name == name);
            if (scm == null)
                return new BotResult($"No scam with name `{name}`");
            await ReplyAsync($"Please provide the index of the trigger to text, or `-1` to add a new, or no reply to cancel",
                embed: embedForScam(scm));
            var nxt = await NextMessageAsync(timeout: TimeSpan.FromSeconds(45));
            if(nxt == null || string.IsNullOrWhiteSpace(nxt.Content))
            {
                await ReplyAsync("Cancelling.");
                return new BotResult();
            }
            if (!int.TryParse(nxt.Content, out int index))
                return new BotResult($"Could not parse {nxt.Content} as an integer");
            if(index == -1)
            {
                await ReplyAsync("Please provide new trigger");
                var trig = await NextMessageAsync(timeout: TimeSpan.FromMinutes(5));
                if (trig == null || string.IsNullOrWhiteSpace(trig.Content))
                    return new BotResult("You did not provide a new trigger in time!");
                scm.Text = scm.Text.Append(trig.Content.ToLower()).ToArray();
                Detector.OnSave();
                await ReplyAsync("Added!");
            } else
            {
                if(index >= 0 && index < scm.Text.Length)
                {
                    var txt = scm.Text[index];
                    await ReplyAsync("Please provide text to replace the following:\r\n>>> " + txt);
                    var reply = await NextMessageAsync(timeout: TimeSpan.FromMinutes(5));
                    if (reply == null)
                        return new BotResult("You did not provide a replacement trigger.");
                    if(string.IsNullOrWhiteSpace(reply.Content))
                    {
                        var ls = scm.Text.ToList();
                        ls.RemoveAt(index);
                        scm.Text = ls.ToArray();
                        await ReplyAsync("Removed trigger.");
                        Detector.OnSave();
                        return new BotResult();
                    }
                    scm.Text[index] = reply.Content.ToLower();
                    Detector.OnSave();
                    await ReplyAsync("Updated!");
                } else
                {
                    return new BotResult("Your index is invalid.");
                }
            }
            return new BotResult();
        }

        [Command("words")]
        [Summary("Provides the words of the image")]
        public async Task<RuntimeResult> ProvideWords(
            [Summary("Forces use of original image; default uses inverted")]bool forceOriginal = false)
        {
            var attch = Context.Message.Attachments.FirstOrDefault();
            if (attch == null)
                return new BotResult("Failed to upload any attachments");
            var uri = new Uri(attch.Url);
            string path = uri.AbsolutePath;
            string file = path.Substring(path.LastIndexOf('/') + 1);
            var temp = Path.Combine(Path.GetTempPath(), file);
            Program.LogMsg("Downloading to " + temp);
            using (WebClient client = new WebClient())
            {
                client.DownloadFile(uri, temp);
            }
            if(!forceOriginal)
            {
                Program.LogMsg("Now inverting image");
                Bitmap pic = new Bitmap(temp);
                for (int y = 0; (y <= (pic.Height - 1)); y++)
                {
                    for (int x = 0; (x <= (pic.Width - 1)); x++)
                    {
                        System.Drawing.Color inv = pic.GetPixel(x, y);
                        inv = System.Drawing.Color.FromArgb(255, (255 - inv.R), (255 - inv.G), (255 - inv.B));
                        pic.SetPixel(x, y, inv);
                    }
                }
                file = $"inverted_{file}";
                temp = Path.Combine(Path.GetTempPath(), file);
                Program.LogMsg($"Inverted image to {temp}");
                pic.Save(temp);
            }
            await performTasks(file);
            var wordFile = Path.Combine(Path.GetTempPath(), $"words_{file}.txt");
            string[] lines;
            try
            {
                lines = File.ReadAllLines(wordFile);
            } catch (Exception ex)
            {
                Program.LogMsg(ex, "WordPrintScams");
                return new BotResult($"Failed to get words, the temporary file may have been deleted");
            }
            var txt = string.Join("\r\n", lines);
            if(txt.Length < 1000)
            {
                await ReplyAsync($"```\r\n{txt}\r\n```");
            } else
            {
                await Context.Channel.SendFileAsync(wordFile, $"Too many charactors to print directly.");
            }
            return new BotResult();
        }

        [Command("modreason"), Alias("mreason")]
        [Summary("Changes the reason of a scam")]
        public async Task<RuntimeResult> ModifyReason([Remainder]string name)
        {
            var scm = Detector.Scams.FirstOrDefault(x => x.Name == name);
            if (scm == null)
                return new BotResult($"No scam with name `{name}`");
            await ReplyAsync("Please provide replacement for:\r\n\r\n>>>" + scm.Reason);
            var rsn = await NextMessageAsync(timeout: TimeSpan.FromMinutes(5));
            if (rsn == null || string.IsNullOrWhiteSpace(rsn.Content))
                return new BotResult("Cancelling.");
            scm.Reason = rsn.Content;
            Detector.OnSave();
            await ReplyAsync("Updated reason");
            return new BotResult();
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
