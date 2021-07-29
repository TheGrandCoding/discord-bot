using Discord;
using Discord.WebSocket;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class LinksThreaderService : SavedService
    {
        public SmmyRatelimit Ratelimit { get; set; } = new SmmyRatelimit();
        public ConcurrentDictionary<ulong, ChannelConfig> Channels { get; set; } = new ConcurrentDictionary<ulong, ChannelConfig>();

        public const string API_URL = @"https://api.smmry.com";

        string getEndpoint(string newsUrl)
        {
            return API_URL +
                "?SM_API_KEY=" + Program.Configuration["tokens:smmry"] +
                "&SM_WITH_BREAK=" +
                // URL MUST BE LAST
                "&SM_URL=" + Uri.EscapeDataString(newsUrl);
        }

        class save
        {
            public SmmyRatelimit ratelimit;
            public Dictionary<ulong, ChannelConfig> channels;
        }
        public override string GenerateSave()
        {
            var c = Channels;
            var sv = new save()
            {
                ratelimit = Ratelimit,
                channels = new Dictionary<ulong, ChannelConfig>(c.ToArray())
            };
            return Program.Serialise(sv);
        }

        public override void OnReady()
        {
            EnsureConfiguration("tokens:smmry");
            var sv = Program.Deserialise<save>(ReadSave());
            Ratelimit = sv.ratelimit ?? new SmmyRatelimit();
            Channels = new ConcurrentDictionary<ulong, ChannelConfig>(sv.channels ?? new Dictionary<ulong, ChannelConfig>());
            Program.Client.MessageReceived += Client_MessageReceived;
        }

        async Task<string> getTitle(Uri uri)
        {
            var hash = Hash.GetSHA1(uri.AbsoluteUri);
            var filename = $"ml_{hash}.html";
            var path = Path.Combine(Path.GetTempPath(), filename);
            var info = new FileInfo(path);
            if(!info.Exists || info.Length < 1024 || (DateTime.Now - info.LastWriteTime).TotalDays > 1)
            {
                var http = Program.Services.GetRequiredService<HttpClient>();
                var response = await http.GetAsync(uri, Program.GetToken());
                var stream = await response.Content.ReadAsStreamAsync();
                using (var file = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(file);
                }
            }
            HtmlDocument doc = new HtmlDocument();
            doc.Load(path);
            var selected = "//title";
            var title = doc.DocumentNode.SelectSingleNode(selected);
            return title?.InnerText ?? uri.Host;
        }

        async Task<smmryResponse> getSummary(Uri uri)
        {
            var apiEndpoint = getEndpoint(uri.ToString());
            var http = Program.Services.GetRequiredService<HttpClient>();
            Ratelimit.AddRequest();
            try
            {
                var response = await http.GetAsync(apiEndpoint);
                var text = await response.Content.ReadAsStringAsync();
                Debug($"{response.StatusCode}: {text}");
                var json = Program.Deserialise<smmryResponse>(text);
                return json;
            }
            finally
            {
                OnSave();
            }
        }

        class smmryResponse
        {
            [JsonProperty("sm_api_message")]
            public string ApiMessage { get; set; }
            [JsonProperty("sm_api_character_count")]
            public int CharacterCount { get; set; }
            [JsonProperty("sm_api_content_reduced")]
            public string ReducedPercentage { get; set; }
            [JsonProperty("sm_api_title")]
            public string Title { get; set; }
            [JsonProperty("sm_api_content")]
            public string Content { get; set; }
            [JsonProperty("sm_api_keyword_array")]
            public string[] Keywords { get; set; }
            [JsonProperty("sm_api_error")]
            public int? ErrorCode { get; set; }
        }

        async Task summarizeLink(IThreadChannel thread, Uri uri, MessageReference messageRef)
        {
            if (Ratelimit.IsDayLimited)
            {
                await thread.SendMessageAsync($"Could not summarize this article - I have hit the maximum 100 requests for the day.",
                    messageReference: messageRef);
                return;
            }
            if (Ratelimit.IsTimeLimited)
            {
                var ts = DateTime.Now - Ratelimit.LastSent;
                Info("Pre-emptive ratelimit invoked; " + ts.Milliseconds + "ms");
                if (ts.TotalMilliseconds > 1) // don't want it to wait forever
                    await Task.Delay(ts);
            }
            var resp = await getSummary(uri);
            if (!resp.ErrorCode.HasValue)
            {
                var summary = resp.Content.Replace("[BREAK]", "\r\n>");
                if (summary.EndsWith(">"))
                    summary = summary.Substring(0, summary.Length - 1);

                summary = $"> {summary} Reduced by {resp.ReducedPercentage}";
                if (summary.Length > 2000)
                {
                    summary = summary.Replace(">", "");
                    await thread.SendMessageAsync(messageReference: messageRef,
                        embed: new EmbedBuilder()
                        .WithDescription(summary).Build());
                }
                else
                {
                    await thread.SendMessageAsync(summary, messageReference: messageRef);
                }
            }
        }

        async Task handle(ISocketMessageChannel channel, SocketMessage msg, Uri uri, bool summarize)
        {
            IThreadChannel thread;
            MessageReference msgRef;
            if(channel is ITextChannel text)
            {
                var title = await getTitle(uri);
                title = Program.Clamp(title, 100);
                thread = await text.CreateThread(msg.Id, x =>
                {
                    x.Name = title;
                    x.AutoArchiveDuration = 1440; // one day
                });
                msgRef = null;
            } else
            {
                thread = channel as IThreadChannel; // they're already in a thread, so we can only summarize.
                // so we'll reply the message instead.
                msgRef = new MessageReference(msg.Id);
            }
            if (summarize)
                await summarizeLink(thread, uri, msgRef);
        }

        private async Task Client_MessageReceived(Discord.WebSocket.SocketMessage arg)
        {
            if(!(arg.Channel is ITextChannel text))
            {
                if(arg.Channel is IThreadChannel thread)
                {
                    text = Program.Client.GetChannel(thread.ChannelId) as ITextChannel;
                } else
                {
                    text = null;
                }
            }
            if (text == null)
                return;
            if (!Channels.TryGetValue(text.Id, out var setting))
                return;
            if (!Uri.TryCreate(arg.Content.Trim(), UriKind.Absolute, out var uri))
                return;

            var x = Task.Run(async () =>
            {
                try
                {
                    await handle(arg.Channel, arg, uri, setting == ChannelConfig.Summary);
                } catch(Exception e)
                {
                    Error(e);
                }
            });
            //x.Start();

            
        }
    }

    [Flags]
    public enum ChannelConfig
    {
        Disabled = 0,
        Thread = 0b01,
        Summary = 0b10 | Thread
    }

    public class SmmyRatelimit
    {
        [JsonProperty("c")]
        private int _count;
        [JsonProperty("d")]
        private DateTime _date;

        [JsonIgnore]
        public DateTime LastSent => _date;

        public SmmyRatelimit()
        {
            _count = 0;
            _date = DateTime.Now.Date;
        }
        [JsonConstructor]
        private SmmyRatelimit(int _Count, DateTime _Date)
        {
            _count = _Count;
            _date = _Date;
            checkDate();
        }
    
        void checkDate()
        {
            if (_date.DayOfYear != DateTime.Now.DayOfYear)
            {
                _date = DateTime.Now.Date;
                _count = 0;
            }
        }

        public int AddRequest()
        {
            checkDate();
            _date = DateTime.Now;
            return ++_count;
        }

        [JsonIgnore]
        public bool IsDayLimited {  get
            {
                checkDate();
                return _count >= 100;
            } }
        [JsonIgnore]
        public bool IsTimeLimited { get
            {
                checkDate();
                return (DateTime.Now - _date).TotalSeconds < 10;
            } }


    }
}
