using DiscordBot.Classes.DbContexts;
using DiscordBot.MLAPI.Attributes;
using DiscordBot.Services;
using DiscordBot.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DiscordBot.MLAPI.Modules
{
    [RequireOwner]
    public class RssModule : AuthedAPIBase
    {
        public RssModule(APIContext context) : base(context, "rss")
        {
            DB = context.Services.GetRssDb($"HTTP_{context.Endpoint.Name}");
            Service = context.Services.GetRequiredService<RssService>();
        }

        public RssService Service { get; set; }
        public RssDbContext DB { get; set; }

        [Method("GET"), Path("/rss")]
        public async Task HtmlIndex()
        {
            await ReplyFile("rss.html", 200);
        }

        [Method("GET"), Path("/api/rss/feeds")]
        public async Task APIGetAllFeeds()
        {
            var feeds = new JArray();
            DB.Feeds.OrderBy(x => x.Id).ToList().ForEach(feed =>
            {
                var jobj = new JObject();
                jobj["id"] = feed.Id;
                jobj["name"] = feed.Name;
                jobj["nextCheck"] = new DateTimeOffset(feed.NextCheck).ToUnixTimeMilliseconds().ToString();
                jobj["interval"] = feed.Interval;
                jobj["parser"] = feed.ParserId;
                jobj["unread"] = feed.UnreadArticles;
                jobj["url"] = feed.Url;
                jobj["filters"] = new JArray(feed.Filters.Select(f => f.FilterId).ToList());
                feeds.Add(jobj);
            });
            await RespondJson(feeds);
        }

        public struct PostFeedData
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Url { get; set; }
            public int? Parser { get; set; }
            public int[] Filters { get; set; }
            public int Interval { get; set; }
        }

        [Method("POST"), Path("/api/rss/feeds")]
        public async Task APIEditFeed([FromBody]PostFeedData data)
        {
            var feed = data.Id == 0 ? new RssFeed() : await DB.Feeds.FindAsync(data.Id);
            if (string.IsNullOrWhiteSpace(data.Name) || string.IsNullOrWhiteSpace(data.Url))
            {
                await RespondRaw("Invalid data.");
                return;
            }
            feed.Name = data.Name;
            feed.Url = data.Url;
            feed.Interval = Math.Max(15, data.Interval);
            if(data.Parser.HasValue)
            {
                var parserScript = await DB.Scripts.FindAsync(data.Parser);
                if(!parserScript.Code.Contains("function parse("))
                {
                    await RespondRaw("Parser script does not define parse() function", 400);
                    return;
                }
            }
            feed.ParserId = data.Parser;
            var filters = new List<RssFeedFilterScript>();
            foreach (var id in (data.Filters ?? new int[0]))
            {
                var script = await DB.Scripts.FindAsync(id);
                if (!script.Code.Contains("function checkFilter("))
                {
                    await RespondRaw($"Filter script {id} does not define checkFilter() function", 400);
                    return;
                }
                filters.Add(new RssFeedFilterScript() { Feed = feed, FilterId = id });
            }
            feed.Filters = filters;
            if(data.Id == 0)
            {
                await DB.Feeds.AddAsync(feed);
            } else
            {
                DB.Feeds.Update(feed);
            }
            feed.NextCheck = DateTime.MinValue;
            await DB.SaveChangesAsync();
            await Service.RunPendingCheck(Context.Services);
            await RespondRedirect("/rss");
        }

        [Method("POST"), Path("/api/rss/refresh/{id}")]
        [Regex("id", "[0-9]+")]
        public async Task ApiStartPendingCheck(int id, bool refilter = false)
        {
            await Service.RunPendingCheck(Context.Services, id, refilter);
            await RespondRaw("");
        }

        [Method("DELETE"), Path("/api/rss/feed/{id}")]
        [Regex("id", "[0-9]+")]
        public async Task ApiDeleteFeed(int id)
        {
            var feed = await DB.Feeds.FindAsync(id);
            DB.Feeds.Remove(feed);
            await DB.SaveChangesAsync();
            await RespondRaw("");
        }

        [Method("GET"), Path("/api/rss/articles/{feed}")]
        [Regex("feed", "[0-9]+")]
        public async Task ApiGetArticles(int feed, string search = null, int? page = null, int? pageSize = null)
        {
            var pg = pageSize.GetValueOrDefault(25);
            var articles = DB.Articles.AsAsyncEnumerable();
            if(feed != 0)
                articles = articles.Where(x => x.FeedId == feed);
            if (search != null)
                articles = articles.Where(x => x.Title.Contains(search) || x.Author == search);

            articles = articles.OrderByDescending(x => x.PublishedDate).ThenByDescending(x => x.SeenDate);
            articles = articles.Skip(page.GetValueOrDefault(0) * pg);
            articles = articles.Take(pg);
            var jarray = new JArray();
            await foreach(var article in articles)
            {
                var jobj = new JObject();
                jobj["id"] = article.Id;
                jobj["feed"] = article.FeedId;
                jobj["title"] = article.Title;
                jobj["url"] = article.Url;
                jobj["author"] = article.Author;
                jobj["read"] = article.IsRead;
                jobj["important"] = article.IsImportant;
                jobj["seenDate"] = article.SeenDate.ToUnixTimeMilliseconds().ToString();
                jobj["pubDate"] = article.PublishedDate.ToUnixTimeMilliseconds().ToString();
                jarray.Add(jobj);
            }
            await RespondJson(jarray);

        }

        public struct PatchArticleData
        {
            public bool? read { get; set; }
            public bool? important { get; set; }
        }

        private async Task<bool> updateArticle(int article, PatchArticleData update)
        {
            var art = await DB.Articles.FindAsync(article);
            if (art == null)
            {
                await RespondRaw("", 404);
                return false;
            }
            if (update.read.HasValue)
            {
                if (art.IsRead != update.read.Value)
                {
                    var feed = await DB.Feeds.FindAsync(art.FeedId);
                    feed.UnreadArticles += art.IsRead ? 1 : -1;
                    DB.Feeds.Update(feed);
                    art.IsRead = update.read.Value;
                }
            }
            if (update.important.HasValue)
            {
                art.IsImportant = update.important.Value;
            }
            DB.Articles.Update(art);
            return true;
        }

        [Method("PATCH"), Path("/api/rss/article/{article}")]
        [Regex("article", "[0-9]+")]
        public async Task ApiUpdateArticle(int article, [FromBody]PatchArticleData update)
        {
            var success = await updateArticle(article, update);
            if(success)
            {
                await DB.SaveChangesAsync();
                await RespondRaw("", 200);
            } else
            {
                await RespondRaw("", 400);
            }
        }


        [Method("PATCH"), Path("/api/rss/articles")]
        public async Task ApiBatchUpdateArticles([FromBody]Dictionary<int, PatchArticleData> update)
        {
            var transaction = await DB.Database.BeginTransactionAsync();
            var success = false;
            try
            {
                foreach ((var key, var value) in update)
                {
                    if (!await updateArticle(key, value))
                    {
                        await RespondRaw("", 400);
                        return;
                    }
                }
                success = true;
            } finally
            {
                if(success)
                {
                    await transaction.CommitAsync();
                    await DB.SaveChangesAsync();
                    await RespondRaw("");
                }
                else
                {
                    await transaction.RollbackAsync();
                }
            }
        }

        public struct TestUrlData
        {
            public string url { get; set; }
        }

        [Method("POST"), Path("/api/rss/test")]
        public async Task ApiTestUrl([FromBody]TestUrlData data)
        {
            (var reader, _) = await Service.GetXmlReaderAsync(data.url, Context.Services.GetHttp());
            var feed = SyndicationFeed.Load(reader);
            var jobj = new JObject();
            jobj["name"] = feed.Title.Text;

            await RespondJson(jobj);
        }
    
        [Method("GET"), Path("/api/rss/scripts")]
        public async Task ApiGetScripts()
        {
            var jarray = new JArray();
            await foreach(var script in DB.Scripts.AsAsyncEnumerable())
            {
                var jobj = new JObject();
                jobj["id"] = script.Id;
                jobj["name"] = script.Name;
                jobj["code"] = script.Code;
                jarray.Add(jobj);
            }
            await RespondJson(jarray);
        }

        public struct EditScriptData
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Code { get; set; }
        }

        [Method("POST"), Path("/api/rss/scripts")]
        public async Task ApiEditScript([FromBody]EditScriptData data)
        {
            if (string.IsNullOrWhiteSpace(data.Name) || string.IsNullOrWhiteSpace(data.Code))
            {
                await RespondRaw("Invalid data.", 400);
                return;
            }
            var script = data.Id == 0 ? new RssScript() : await DB.Scripts.FindAsync(data.Id);
            script.Name = data.Name;
            script.Code = data.Code;
            if(data.Id == 0)
            {
                await DB.Scripts.AddAsync(script);
            }
            await DB.SaveChangesAsync();
            await RespondRaw("");
        }
    
        [Method("DELETE"), Path("/api/rss/script/{id}")]
        [Regex("id", "[0-9]+")]
        public async Task ApiDeleteScript(int id)
        {
            var script = await DB.Scripts.FindAsync(id);
            DB.Scripts.Remove(script);
            await DB.SaveChangesAsync();
            await RespondRaw("");
        }
    }
}
