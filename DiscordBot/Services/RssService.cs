using Discord;
using DiscordBot.Classes;
using DiscordBot.MLAPI;
using DiscordBot.Utils;
using Jint;
using Jint.Native.Object;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace DiscordBot.Services
{
    public class RssService : Service
    {
        delegate Jint.Parser.Ast.Program GetScript(RssScript script);
        async IAsyncEnumerable<RssArticle> GetArticles(BotHttpClient http, RssFeed feed, DateTimeOffset nowTime, GetScript getScript)
        {
            (var reader, var content) = await GetXmlReaderAsync(feed.Url, http);
            if(feed.Parser != null)
            {
                var script = getScript(feed.Parser);
                var engine = new Engine();
                var feedObj = new ObjectInstance(engine);
                feedObj.FastAddProperty("id", feed.Id, false, false, false);
                feedObj.FastAddProperty("text", content, false, false, false);
                engine.SetValue("feed", feedObj);
                engine.Execute(script);
                engine.Execute("parse(feed)");
                var value = engine.GetCompletionValue();
                var arr = value.AsArray();
                foreach((var key, var item) in arr.GetOwnProperties())
                {
                    yield return null;
                }
            } else
            {
                var sync = SyndicationFeed.Load(reader);
                foreach(var item in sync.Items)
                {
                    var url = (item.BaseUri ?? item.Links.FirstOrDefault()?.GetAbsoluteUri())?.ToString() ?? item.Id;
                    yield return new RssArticle()
                    {
                        CustomId = item.Id ?? url,
                        FeedId = feed.Id,
                        Title = item.Title.Text,
                        PublishedDate = item.PublishDate,
                        SeenDate = nowTime,
                        Author = string.Join(", ", item.Authors.Select(x => x.Name)),
                        Url = url
                    };
                }
            }
        }

        bool checkFilters(RssFeed feed, RssArticle article, GetScript script)
        {
            foreach(var feedFilter in feed.Filters)
            {
                var filter = feedFilter.Filter;
                if (filter == null) continue;
                var oldId = article.Id;
                var engine = new Engine();
                engine.SetValue("article", article);
                engine.Execute(script(filter));
                engine.Execute("checkFilter()");
                article.Id = oldId;
                article.FeedId = feed.Id;

                var value = engine.GetCompletionValue();
                if (value.IsNull()) continue;
                return value.AsBoolean();
            }
            return true;
        }

        private async Task InternalRunCheck(IServiceProvider services, int? onlyFor = null, bool refilter = false)
        {
            var scriptCache = new Dictionary<int, Jint.Parser.Ast.Program>();
            var parser = new Jint.Parser.JavaScriptParser();
            GetScript parseScript = (script) =>
            {
                if (!scriptCache.TryGetValue(script.Id, out var program))
                {
                    program = parser.Parse(script.Code);
                    scriptCache[script.Id] = program;
                }
                return program;
            };

            var db = services.GetRssDb($"Check ({onlyFor}, {refilter})");
            var http = services.GetRequiredService<DiscordBot.Classes.BotHttpClient>();

            var nowTime = DateTimeOffset.UtcNow;
            List<RssFeed> pending;
            if (onlyFor.HasValue)
            {
                pending = new List<RssFeed>();
                if (onlyFor.Value == 0) pending = await db.Feeds.ToListAsync();
                else pending.Add(await db.Feeds.FindAsync(onlyFor.Value));
            }
            else
            {
                pending = await db.GetPendingFeeds();
            }
            var embed = new EmbedBuilder();
            embed.Title = "RSS";
            var webLink = Handler.LocalAPIUrl + Handler.GetEndpoint(nameof(MLAPI.Modules.RssModule.HtmlIndex)).GetFormattablePath();
            foreach (var feed in pending)
            {
                if (string.IsNullOrWhiteSpace(feed.Url)) continue;
                var articles = GetArticles(http, feed, nowTime, parseScript);
                var toAdd = new List<RssArticle>();
                int newUnread = 0;
                await foreach (var article in articles)
                {
                    var existing = await db.Articles.FirstOrDefaultAsync(x => x.CustomId == article.CustomId);
                    if (existing != null)
                    {
                        if (article.Title != existing.Title
                            || article.Author != existing.Author
                            || article.Url != existing.Url
                            || article.PublishedDate != existing.PublishedDate
                            )
                        {
                            existing.Title = article.Title;
                            existing.Author = article.Author;
                            existing.Url = article.Url;
                            existing.PublishedDate = article.PublishedDate;
                        }
                        continue;
                    }
                    var result = checkFilters(feed, article, parseScript);
                    if (!result) continue;
                    if (!article.IsRead) newUnread++;
                    toAdd.Add(article);
                }
                await db.AddArticles(feed, toAdd);
                if (refilter)
                {
                    int unread = 0;
                    var existing = db.Articles.Where(x => x.FeedId == feed.Id).AsAsyncEnumerable();
                    await foreach (var article in existing)
                    {
                        var already = toAdd.FirstOrDefault(t => t.CustomId == article.CustomId);
                        if (already != null)
                        {
                            if (!already.IsRead) unread++;
                            continue;
                        }

                        var result = checkFilters(feed, article, parseScript);
                        if (!result)
                        {
                            db.Articles.Remove(article);
                        }
                        else if (!article.IsRead)
                        {
                            unread++;
                        }
                    }
                    feed.UnreadArticles = unread;
                }
                if(newUnread > 0)
                {
                    embed.AddField(feed.Name, $"[{newUnread} new unread]({webLink}#{feed.Id})");
                }
            }
            await db.SaveChangesAsync();
            if(embed.Fields.Count > 0)
            {
                await Program.SendLogMessageAsync(embed: embed.Build());
            }
        }

        static SemaphoreSlim _lock = new(1, 1);
        public async Task RunPendingCheck(IServiceProvider services, int? onlyFor = null, bool refilter = false)
        {
            await _lock.WaitAsync();
            try
            {
                await InternalRunCheck(services, onlyFor, refilter);
            } catch(Exception ex)
            {
                Error(ex, "PendingCheck");
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<(XmlReader, string)> GetXmlReaderAsync(string url, BotHttpClient http)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Ignore;
            settings.MaxCharactersFromEntities = 1024;
            var result = await http.GetAsync(url);
            var content = await result.Content.ReadAsStringAsync();
            return (XmlReader.Create(new System.IO.StringReader(content), settings), content);
        }

#if !DEBUG
        [Cron("*", "*/15")]
        public void startScheduledCheck()
        {
            Task.Run(async () =>
            {
                using var scope = Program.GlobalServices.CreateScope();
                await RunPendingCheck(scope.ServiceProvider);
            });
        }
#endif
    }

    public class RssFeed
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }

        public DateTime NextCheck { get; set; }
        public int Interval { get; set; }

        public int UnreadArticles { get; set; }

        public List<RssArticle> Articles { get; set; } = new();

        public RssScript Parser { get; set; }
        public int? ParserId { get; set; }
        public List<RssFeedFilterScript> Filters { get; set; } = new();
    }

    public class RssArticle
    {
        public int Id { get; set; }
        public int FeedId { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string CustomId { get; set; }
        public string Author { get; set; }

        public bool IsRead { get; set; }
        public bool IsImportant { get; set; }

        public DateTimeOffset PublishedDate { get; set; }
        public DateTimeOffset SeenDate { get; set; }
    }

    public class RssFeedFilterScript
    {
        public RssFeed Feed { get; set; }
        public int FeedId { get; set; }
        public RssScript Filter { get; set; }
        public int FilterId { get; set; }
    }

    public class RssScript
    {
        public int Id { get; set; }
        public string Name { get; set; }
        [MaxLength(int.MaxValue)]
        public string Code { get; set; }
    }
}
