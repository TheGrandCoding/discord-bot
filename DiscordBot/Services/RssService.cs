using DiscordBot.Classes;
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
    public class RssDbContext : DbContext
    {
        public DbSet<RssFeed> Feeds { get; set; }
        public DbSet<RssArticle> Articles { get; set; }
        public DbSet<RssScript> Scripts { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
#if DEBUG
            options.EnableSensitiveDataLogging();
#endif
            options.WithSQLConnection("botRss", true);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<RssFeedFilterScript>(b =>
            {
                b.HasKey(p => new { p.FeedId, p.FilterId });
                b.HasOne(p => p.Feed).WithMany(p => p.Filters)
                    .HasForeignKey(p => p.FeedId)
                    .HasPrincipalKey(p => p.Id);
                b.HasOne(p => p.Filter).WithMany()
                    .HasForeignKey(p => p.FilterId)
                    .HasPrincipalKey(p => p.Id);

                b.Navigation(p => p.Feed).AutoInclude();
                b.Navigation(p => p.Filter).AutoInclude();
            });
            builder.Entity<RssFeed>(b =>
            {
                b.HasMany(p => p.Articles).WithOne()
                    .HasForeignKey(p => p.FeedId);
                b.HasOne(p => p.Parser).WithMany()
                    .HasForeignKey(p => p.ParserId)
                    .HasPrincipalKey(p => p.Id);

                b.Navigation(p => p.Parser).AutoInclude();
                b.Navigation(p => p.Filters).AutoInclude();
            });
        }

        public ValueTask<List<RssFeed>> GetPendingFeeds()
        {
            var now = DateTime.UtcNow;
            return Feeds.AsAsyncEnumerable().Where(x => x.NextCheck < now).ToListAsync();
        }

        public async Task AddArticles(RssFeed feed, IEnumerable<RssArticle> articles)
        {
            await Articles.AddRangeAsync(articles);
            var unreads = articles.Count(x => x.IsRead == false);
            feed.UnreadArticles += unreads;
            feed.NextCheck = DateTime.UtcNow.AddMinutes(Math.Min(1, feed.Interval));
            Feeds.Update(feed);
        }
    }
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

            var db = services.GetRssDb();
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
            foreach (var feed in pending)
            {
                if (string.IsNullOrWhiteSpace(feed.Url)) continue;
                var articles = GetArticles(http, feed, nowTime, parseScript);
                var toAdd = new List<RssArticle>();
                await foreach (var article in articles)
                {
                    bool existing = await db.Articles.Where(x => x.CustomId == article.CustomId).AnyAsync();
                    if (existing) continue;
                    var result = checkFilters(feed, article, parseScript);
                    if (!result) continue;
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
            }
            await db.SaveChangesAsync();
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
            Task.Run(() =>
            {
                using var scope = Program.GlobalServices.CreateScope();
                return RunPendingCheck(scope.ServiceProvider);
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
