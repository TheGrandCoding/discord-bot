using Newtonsoft.Json;
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
using Reddit.Inputs.Search;

namespace DiscordBot.Services
{
    public class ScamDetector : SavedService
    {
        public string Folder => Path.Combine(Program.BASE_PATH, "tessdata");
        private Tesseract _ocr;
        private RedditClient reddit;
        private Reddit.Controllers.Subreddit subReddit;
        bool isMod = false;

        private IUser adminManual;

        public List<Scam> Scams = new List<Scam>();
        public List<string> DoneIds = new List<string>();

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
            adminManual ??= Program.Client.GetApplicationInfoAsync().Result.Owner;
            if (_ocr != null)
                return; // already initiallised.
            if(!InitOcr(Folder, "eng", OcrEngineMode.TesseractLstmCombined))
                throw new Exception("Failed to initialised ScamDetector.");
            //_ocr.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ-1234567890");
            var save = Program.Deserialise<SaveInfo>(ReadSave());
            Scams = save.scams ?? new List<Scam>();
            var dlt = DateTime.Now.IsDaylightSavingTime();
            DoneIds = new List<string>();

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
#if DEBUG
            string sub = "mlapi";
#else
            string sub = "DiscordApp";
#endif
            subReddit = reddit.Subreddit(sub).About();
            subReddit.Posts.GetNew();
            subReddit.Posts.NewUpdated += Posts_NewUpdated;
            subReddit.Posts.MonitorNew();
            isMod = subReddit.Moderators.Any(x => x.Id == reddit.Account.Me.Id);
            Program.LogMsg($"Monitoring {subReddit.Name}, {(isMod ? "is a moderator" : "not a mod")}");
            checkDeletePosts();
            Program.Client.ReactionAdded += Client_ReactionAdded;
            var th = new Thread(messageInboxForwarder);
            th.Start();
        }

        private void messageInboxForwarder()
        {
            List<string> PmsHandled = new List<string>();
            while(reddit != null)
            {
                var unread = reddit.Account.Messages.Unread;
                reddit.Account.Messages.MarkAllRead();
                foreach(var thing in unread)
                {
                    if (PmsHandled.Contains(thing.Id))
                        continue;
                    PmsHandled.Add(thing.Id);
                    adminManual.SendMessageAsync(embed: new EmbedBuilder()
                        .WithTitle("Forward /u/" + thing.Author)
                        .WithDescription(thing.Body)
                        .Build());
                }
                Thread.Sleep(60 * 1000);
            }
        }

        private async System.Threading.Tasks.Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> arg1, Discord.WebSocket.ISocketMessageChannel arg2, Discord.WebSocket.SocketReaction arg3)
        {
            var msg = await arg1.GetOrDownloadAsync();
            msg ??= (IUserMessage)(await arg2.GetMessageAsync(arg1.Id));
            if (msg == null)
                return;
            if (msg.Author.Id != Program.Client.CurrentUser.Id)
                return;
            var embd = msg.Embeds.FirstOrDefault();
            if (embd == null || string.IsNullOrWhiteSpace(embd.Url) || !embd.Url.Contains("reddit.com"))
                return;
            makeCommentOnPost(embd.Url, "Hi!  \r\nIn case you didn't know, an image in your post seems to be of a common DM scam, beware!" +
                "\r\n\r\n" + embd.Description);
            await msg.ModifyAsync(x =>
            {
                x.Embed = embd.ToEmbedBuilder().AddField("Sent", "Comment Sent", true).Build();
            });
        }

        void checkDeletePosts()
        {
            var hist = reddit.Account.Me.GetCommentHistory(sort: "new");
            foreach(var msg in hist.Take(25))
            {
                if(msg.Score <= 0)
                {
                    Program.LogMsg($"Removing {msg.Permalink} on {msg.Root.Permalink} as score is {msg.Score}");
                    msg.Delete();
                }
            }
        }

        private void Posts_NewUpdated(object sender, Reddit.Controllers.EventArgs.PostsUpdateEventArgs e)
        {
            using(var client = new WebClient())
            {
                reddit.Account.Messages.MarkAllRead();
                foreach(var post in e.NewPosts)
                {
                    if (DoneIds.Contains(post.Id))
                        continue;
                    DoneIds.Add(post.Id);
                    handleRedditPost(post, client);
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

        Scam testShouldPost = new Scam()
        {
            Name = "",
            Reason = "",
            Text = new string[]
            {
                "Is this real?", "Is this a scam?",
                "Guys this is a scam right?",
                "I feel like this is fake and probably a scam. Has anyone gotten this?",
                "Has anyone else gotten this message?",
                "scam bot. is it?",
                "is a scam",
                "is it?",
            }
        };
        bool shouldSendRedditPost(Post post, EmbedBuilder builder)
        { // this assumes we know its a scam, but are they asking if it is?
            var r = testShouldPost.PercentageMatch(post.Title.ToLower().Split(' '));
            builder.AddField("Maybe Send", $"{(r * 100):00.0}");
#if DEBUG
            if (post.Subreddit != "mlapi")
                return false;
#endif
#if !DEBUG
            // Temporarily refuse all comments - will require manual approval.
            return false;
#endif
            return r >= 0.9;
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
            bool sendReddit = shouldSendRedditPost(post, builder);
            builder.AddField($"Sending to reddit?", sendReddit ? "Yes" : "No");
            var r = adminManual.SendMessageAsync(embed: builder.Build()).Result;
            if(sendReddit)
            {
                makeCommentOnPost(post, $"Hi!  \r\nThe image(s) you've submitted appear to contain a common DM scam" +
                $"\r\n\r\n{mkdown}");
            } else
            {
                r.AddReactionAsync(Emojis.WHITE_CHECK_MARK);
            }
        }

        public Post FromPermalink(string permalink)
        {
            // Get the ID from the permalink, then preface it with "t3_" to convert it to a Reddit fullname.  --Kris
            Match match = Regex.Match(permalink, @"\/comments\/([a-z0-9]+)\/");

            string postFullname = "t3_" + (match != null && match.Groups != null && match.Groups.Count >= 2
                ? match.Groups[1].Value
                : "");
            if (postFullname.Equals("t3_"))
            {
                throw new Exception("Unable to extract ID from permalink.");
            }

            // Retrieve the post and return the result.  --Kris
            return reddit.Post(postFullname).About();
        }

        void makeCommentOnPost(string link, string content)
        {
            var post = FromPermalink(link);
            if (post == null)
                throw new NullReferenceException("Unable to find post with that URL");
            makeCommentOnPost(post, content);
        }

        Comment getPreviousComment(Post post)
        {
            var history = reddit.Account.Me.GetCommentHistory().Take(25);
            foreach(var pst in history)
            {
                if (pst.Root.Id == post.Id)
                    return pst;
            }
            return null;
        }

        void makeCommentOnPost(Post post, string content)
        {
            var previous = getPreviousComment(post);
            if(previous != null)
            {
                previous.Edit(content);
                return;
            }
            post.Reply(content);
            if (isMod)
                post.DistinguishAsync("yes");
        }

        public override string GenerateSave()
        {
            var sve = new SaveInfo()
            {
                scams = Scams,
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
        public Scam () { }

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
