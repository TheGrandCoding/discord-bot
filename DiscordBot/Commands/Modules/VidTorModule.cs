using BencodeNET.Parsing;
using BencodeNET.Torrents;
using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using qBitApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("recording"), Alias("recordings", "record")]
    [Summary("Lesson Recordings Torrent")]
    public class VidTorModule : BotBase
    {
        public VidTorService Service { get; set; }
        [Command("upload")]
        public async Task<RuntimeResult> Upload()
        {
            if (Context.Message.Attachments.Count == 0)
                return Error("You must upload the .torrent file.");
            var attch = Context.Message.Attachments.First();
            var temp = Path.Combine(Path.GetTempPath(), attch.Filename);
            using (var wc = new WebClient())
                wc.DownloadFile(attch.Url, temp);
            var parse = new BencodeParser();
            var torrent = parse.Parse<Torrent>(temp);
            if (!torrent.IsPrivate)
                return Error("Torrent must be set to private.");
            if (torrent.FileMode != TorrentFileMode.Single)
                return Error("Torrent must be a singular file");
            var match = Regex.Match(Path.GetFileNameWithoutExtension(torrent.File.FileName), VidTorService.NamePattern);
            if (!match.Success)
                return Error("Video file name must match exactly 'yyyy-MM-dd_Px' format.");
            if (torrent.Trackers.Count != 1)
                return Error("There should be exactly one tracker: you.");
            var trackers = torrent.Trackers[0];
            if (trackers.Count != 1)
                return Error("There should be exactly one tracker: you");
            if (!Uri.TryCreate(trackers[0], UriKind.Absolute, out var uri))
                return Error("Tracker must be a http/https link to your public IP address.");
            var client = Program.Services.GetRequiredService<HttpClient>();
            var request = new HttpRequestMessage(HttpMethod.Options, trackers[0]);
            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch (HttpRequestException req)
            {
                return Error($"Could not connect to URL - make sure your ports are open, and your torrent program is too\r\n*{req.Message}*");
            }
            if (response.StatusCode != HttpStatusCode.BadRequest)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Error($"Irregular response received: {response.StatusCode}\r\n```\r\n{content}\r\n```");
            }
            (string error, string className) = await CheckValidLessonName(torrent);
            if (error != null)
                return Error(error);
            className = className.Replace("/", "");
            await ReplyAsync($"Torrent accepted as valid. Class determine as **{className}**\r\nSending to download client...");
            await Service.AddNew(temp, x =>
            {
                x.Lesson = className;
                x.UploadedBy = Context.BotUser;
            });

            return await Success("Torrent has been sent to download client. You will receive messages as it progresses.");
        }

        [Command("whitelist")]
        [RequireOwner]
        public async Task Whitelist(IPAddress address)
        {
            var ht = Path.Combine(VidTorService.BasePath, ".htaccess");
            try
            {
                var existing = File.ReadAllLines(ht);
                int l = 0;
                foreach(var line in existing)
                {
                    l++;
                    if(line.StartsWith("Require ip"))
                    {
                        if(line.Contains(address.ToString()))
                        {
                            await ReplyAsync($"That IP is already whitelisted on line {l:00}, you'll need to manually remove it");
                            return;
                        }
                    }
                }
            } catch(FileNotFoundException)
            {
                string text = "ErrorDocument 403 https://ml-api.uk.ms/whitelist" + Environment.NewLine;
                text += "Require ip 192.168.1 192.168.0" + Environment.NewLine;
                File.WriteAllText(ht, text);
            }
            File.AppendAllText(ht, "Require ip " + address.ToString() + Environment.NewLine);
            await ReplyAsync("Added to whitelist.");
        }

        async Task<(string, string)> CheckValidLessonName(Torrent torrent)
        {
            if(torrent.Comment != null)
            {
                var mtch = Regex.Match(torrent.Comment, VidTorService.LessonPattern);
                if (!mtch.Success)
                    return ("If specified, torrent comment must contain text with similar pattern to '13BMt1'", null);
                return (null, mtch.Value);
            }
            if(Context.Guild == null)
                return ("You must run this command either in the Homework channel, or specify the class name in the torrent's Comment field in the form similar to '13BMt1'", null);
            var subjectName = Context.Channel.Name;
            foreach(var keypair in Context.BotUser.Classes)
            {
                if(keypair.Value.ToLower() == subjectName)
                {
                    return (null, keypair.Key);
                }
            }
            return ("Could not determine lesson subject. Please specify the class name in the torrent's Comment field.", null);
        }
    }
}
