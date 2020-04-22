﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Emgu.CV.OCR;
using System.Text.RegularExpressions;
using Emgu.CV;
using Discord;
using System.Threading;
using System.Net;
using System.Collections.Immutable;
using Reddit;
using Reddit.Controllers;

namespace DiscordBot.Services
{
    public class ScamDetector : SavedService
    {
        public string Folder => Path.Combine(Program.BASE_PATH, "tessdata");
        private Tesseract _ocr;
        private RedditClient reddit;
        private Reddit.Controllers.Subreddit subReddit;
        bool isMod = false;
        private IUser Temp => Program.Client.GetUser(144462654201790464);

        public List<Scam> Scams = new List<Scam>();
        public DateTime LastKnown { get; set; }

        private static void TesseractDownloadLangFile(String folder, String lang)
        {
            //String subfolderName = "tessdata";
            //String folderName = System.IO.Path.Combine(folder, subfolderName);
            String folderName = folder;
            if (!System.IO.Directory.Exists(folderName))
            {
                System.IO.Directory.CreateDirectory(folderName);
            }
            String dest = System.IO.Path.Combine(folderName, String.Format("{0}.traineddata", lang));
            if (!System.IO.File.Exists(dest))
                using (System.Net.WebClient webclient = new System.Net.WebClient())
                {
                    String source = Emgu.CV.OCR.Tesseract.GetLangFileUrl(lang);

                    Console.WriteLine(String.Format("Downloading file from '{0}' to '{1}'", source, dest));
                    webclient.DownloadFile(source, dest);
                    Console.WriteLine(String.Format("Download completed"));
                }
        }

        private bool InitOcr(String path, String lang, OcrEngineMode mode)
        {
            try
            {
                if (_ocr != null)
                {
                    _ocr.Dispose();
                    _ocr = null;
                }

                if (String.IsNullOrEmpty(path))
                    path = Emgu.CV.OCR.Tesseract.DefaultTesseractDirectory;

                TesseractDownloadLangFile(path, lang);
                TesseractDownloadLangFile(path, "osd"); //script orientation detection
                /*
                String pathFinal = path.Length == 0 || path.Substring(path.Length - 1, 1).Equals(Path.DirectorySeparatorChar.ToString())
                    ? path
                    : String.Format("{0}{1}", path, System.IO.Path.DirectorySeparatorChar);
                */
                _ocr = new Tesseract(path, lang, mode);

                Console.WriteLine(String.Format("{0} : {1} (tesseract version {2})", lang, mode.ToString(), Emgu.CV.OCR.Tesseract.VersionString));
                return true;
            }
            catch (Exception ex)
            {
                _ocr = null;
                Program.LogMsg(ex, "Attempt");
                return false;
            }
        }

        public List<ScamResult> getPossibleScams(string testImagePath, out string[] words)
        {
            words = null;
            StringBuilder strBuilder = new StringBuilder();
            try
            {
                var pix = new Pix(new Mat(testImagePath));
                _ocr.SetImage(pix);
                _ocr.Recognize();
                var chrs = _ocr.GetCharacters();
                for (int i = 0; i < chrs.Length; i++)
                    strBuilder.Append(chrs[i].Text);
            }
            catch (Exception e)
            {
                Program.LogMsg("Unexpected Error: " + e.ToString(), Discord.LogSeverity.Error, "Scam");
                return null;
            }
            var text = strBuilder.ToString().ToLower();
            words = Regex.Split(text, @"\s");

            List<ScamResult> possible = new List<ScamResult>();
            foreach(var scam in Scams)
            {
                var thisScam = scam.PercentageMatch(words);
                Program.LogMsg($"{scam.Name}: {(thisScam * 100):00.0}%");
                possible.Add(new ScamResult()
                {
                    Scam = scam,
                    Confidence = thisScam,
                });
            }
            return possible;
        }

        public override void OnReady()
        {
            if(!InitOcr(Folder, "eng", OcrEngineMode.TesseractLstmCombined))
                throw new Exception("Failed to initialised ScamDetector.");
            //_ocr.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ-1234567890");
            var save = Program.Deserialise<SaveInfo>(ReadSave());
            Scams = save.scams ?? new List<Scam>();
            var dlt = DateTime.Now.IsDaylightSavingTime();
            LastKnown = save.lastChanged ?? DateTime.Now.ToUniversalTime();

#if WINDOWS
            string uAgent = $"windows:mlapi-ds:v{Program.VERSION}";
#else
            string uAgent = $"linux:mlapi-ds:v{Program.VERSION}";
#endif

            reddit = new RedditClient(
                appId: Program.Configuration["tokens:reddit:id"],
                refreshToken: Program.Configuration["tokens:reddit:refresh"],
                appSecret: Program.Configuration["tokens:reddit:secret"]);
            Program.LogMsg($"Logged in as {reddit.Account.Me.Name}", LogSeverity.Warning);
            subReddit = reddit.Subreddit("mlapi").About();
            subReddit.Posts.GetNew();
            subReddit.Posts.NewUpdated += Posts_NewUpdated;
            subReddit.Posts.MonitorNew();
            isMod = subReddit.Moderators.Any(x => x.Id == reddit.Account.Me.Id);
            Program.LogMsg($"Monitoring {subReddit.Name}, {(isMod ? "is a moderator" : "not a mod")}");

        }

        private void Posts_NewUpdated(object sender, Reddit.Controllers.EventArgs.PostsUpdateEventArgs e)
        {
            using(var client = new WebClient())
            {
                foreach(var post in e.NewPosts)
                {
                    if (post.Created.ToUniversalTime() < LastKnown.ToUniversalTime())
                        continue;
                    handleRedditPost(post, client);
                    if (post.Created > LastKnown)
                        LastKnown = post.Created;
                }
            }
        }

        private string[] images = new string[]
        {
            "jpg", "jpeg", "png"
        };
        bool isImageUrl(Uri url)
        {
            var filename = Path.GetFileName(url.AbsolutePath);
            var extension = filename.Substring(filename.LastIndexOf('.') + 1);
            return images.Contains(extension);
        }

        const string pattern = @"https?:\/\/[\w\-%\.\/\=\?\&]+";
        List<Uri> getImageUrls(Post post)
        {
            var urls = new List<Uri>();
            if (post is LinkPost link)
                return new List<Uri>() { new Uri(link.URL) };
            if(post is SelfPost self)
            {
                var matches = Regex.Matches(self.SelfText, pattern);
                foreach(Match mtch in matches)
                {
                    urls.Add(new Uri(mtch.Groups[0].Value));
                }
            }
            return urls;
        }

        void handleRedditPost(Post post, WebClient client)
        {
            var uris = getImageUrls(post);
            var images = uris.Where(x => isImageUrl(x)).ToList();
            if (images.Count == 0)
                return;
            Program.LogMsg($"{post.Created} {post.Author}: {post.Title}", LogSeverity.Warning);
            var foundScams = new List<ScamResult>();
            foreach (var x in images)
            {
                Program.LogMsg($"Finding scams for {x}");
                var filename = Path.GetFileName(x.AbsolutePath);
                var temp = Path.Combine(Path.GetTempPath(), filename);
                client.DownloadFile(x, temp);
                var scams = getPossibleScams(temp, out var words);
                File.WriteAllLines(Path.Combine(Path.GetTempPath(), $"words_{filename}.txt"), words);
                var isScam = scams.Any(x => x.Confidence >= 0.9);
                if (isScam)
                {
                    foundScams = scams.Where(x => x.Confidence >= 0.9).ToList();
                    break; // dont bother looking at more images
                }
            }
            if (foundScams.Count == 0)
                return;

            string mkdown = $"";
            foreach (var scam in foundScams)
            {
                mkdown += $"{scam.Scam.Name}";
                if (scam.Scam.Reason != null)
                    mkdown += ": " + scam.Scam.Reason;
                mkdown += "\n\n";
            }
            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle("Looks like a scam!")
                .WithDescription(mkdown)
                .WithUrl($"https://www.reddit.com{post.Permalink}");
            if (post is LinkPost ps && isImageUrl(new Uri(ps.URL)))
                builder.ThumbnailUrl = ps.URL;
            Temp.SendMessageAsync(embed: builder.Build());

            post.Reply($"Hi!  \r\nThe images you have submitted appear to contain a scam  \r\n" +
                $"Information about it is below:\r\n\r\n{mkdown}").Submit();
            if (isMod)
                post.DistinguishAsync("yes");
        }

        public override string GenerateSave()
        {
            var sve = new SaveInfo()
            {
                scams = Scams,
                lastChanged = LastKnown
            };
            return Program.Serialise(sve);
        }
    }

    public class ScamResult
    {
        public double Confidence { get; set; }
        public Scam Scam { get; set; }
    }

    public class Scam
    {
        [JsonProperty("text")]
        public string[] Text { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("reason", NullValueHandling = NullValueHandling.Include)]
        public string Reason { get; set; }

        [JsonConstructor]
        private Scam(string[] text)
        {
            Text = text.Select(x => x.ToLower()).ToArray();
        }

        int numWordsContained(string[] words, string[] TestWords)
        {
            int numWords = 0;
            foreach (var test in TestWords)
            {
                if (words.Contains(test))
                    numWords++;
            }
            return numWords;
        }

        int numPhrasesInOrder(List<string> words, string[] TestWords)
        {
            int current = 0;
            for(int testing = 0; testing < TestWords.Length; testing++)
            {
                for(int y = 0; y < words.Count && testing < TestWords.Length; y++)
                {
                    var word = TestWords[testing];
                    var against = words[y];
                    if (against == "")
                        continue;
                    if (word == against)
                    {
                        current++;
                        testing++;
                    }
                }
            }
            return current;
        }

        public double PercentageMatch(string[] words)
        {
            double highest = 0;
            int i = 0;
            foreach(var possible in Text)
            {
                var split = possible.Split(' ');
                var contains = numWordsContained(words, split);
                var inOrder = numPhrasesInOrder(words.ToList(), split);
                var total = contains + inOrder;
                var perc = (double)total / (split.Length * 2);
                if (perc > highest)
                    highest = perc;
            }
            return highest;
        }
    }

    public class SaveInfo
    {
        public List<Scam> scams;
        public DateTime? lastChanged;
    }
}
