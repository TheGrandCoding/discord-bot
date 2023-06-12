using DiscordBot.Services;
using DiscordBot.Utils;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Classes.DbContexts
{
    public sealed class RssDbContext : AbstractDbBase
    {
        private static int _count = 0;
        private static SemaphoreSlim _semaphore = new(1, 1);
        protected override int _lockCount { get => _count; set => _count = value; }
        protected override SemaphoreSlim _lock => _semaphore;


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
            feed.NextCheck = DateTime.UtcNow.AddMinutes(Math.Max(1, feed.Interval));
            Feeds.Update(feed);
        }
    }
}
