using DiscordBot.Classes;
using DiscordBot.Services;
using DiscordBot.Websockets;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using DiscordBot.Utils;

namespace DiscordBot.Services
{
    public class TimeTrackService : Service
    {
        public override void OnReady()
        {
#if DEBUG
            return;
#endif
            using var db = Program.Services.GetRequiredService<TimeTrackDb>();
            if(PurgeMonthOldThreads(db))
            {
                db.SaveChanges();
            }
        }

        public bool PurgeMonthOldThreads(TimeTrackDb db)
        {
            if (Program.DailyValidateFailed())
                return false;


            // Remove ALL threads older than one month
            var cutoff = DateTime.Now.Date.AddMonths(-1);
            var oldThreads = db.Threads.AsQueryable()
                .Where(x => x.LastUpdated < cutoff)
                .ToList();
            if (oldThreads.Count > 0)
            {
                Warning($"Purging {oldThreads.Count} old threads");
            }
            db.Threads.RemoveRange(oldThreads);
            return oldThreads.Count > 0;
        }
    }


    public class TimeTrackFactory : IDesignTimeDbContextFactory<TimeTrackDb>
    {
        public TimeTrackDb CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<TimeTrackDb>();
            builder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=watch;MultipleActiveResultSets=true");
            builder.EnableSensitiveDataLogging();
            return new TimeTrackDb(builder.Options);
        }
    }
    public class TimeTrackDb : DbContext
    {
        public TimeTrackDb(DbContextOptions<TimeTrackDb> options) : base(options)
        {
        }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.WithSQLConnection("watch");
        }

        private static Cached<string> ExtensionVersion { get; set; } = new Cached<string>(null, 180);
        private static CacheDictionary<string, Video> VideoInfo { get; set; } = new CacheDictionary<string, Video>(1440);

        public static void SetExtVersion(string version)
        {
            ExtensionVersion.Value = version;
        }

        public static Dictionary<string, Video> GetVideoInformation(params string[] videoIds)
        {
            var toRequest = new List<string>();
            var dict = new Dictionary<string, Video>();
            foreach (var id in videoIds)
            {
                if (VideoInfo.TryGetValue(id, out var v))
                    dict[id] = v;
                else
                    toRequest.Add(id);
            }
            if (toRequest.Count == 0)
                return dict;
            var client = Program.Services.GetRequiredService<YouTubeService>();
            var request = client.Videos.List("snippet");
            request.Id = new Google.Apis.Util.Repeatable<string>(toRequest);
            var response = request.Execute();
            foreach (var videos in response.Items)
            {
                dict[videos.Id] = videos;
                VideoInfo[videos.Id] = videos;
            }
            return dict;
        }

        public static string GetExtensionVersion(bool bypassCache = false)
        {
            if (bypassCache || ExtensionVersion.GetValueOrDefault(null) == null)
            {
                var client = Program.Services.GetRequiredService<Classes.BotHttpClient>();
                var r = client.GetAsync("https://api.github.com/repos/CheAle14/time-tracker/releases/latest").Result;
                if (r.IsSuccessStatusCode)
                {
                    var jobj = Newtonsoft.Json.Linq.JObject.Parse(r.Content.ReadAsStringAsync().Result);
                    var s = jobj["tag_name"].ToObject<string>();
                    if (s.StartsWith("v"))
                        s = s[1..];
                    TimeTrackDb.ExtensionVersion.Value = s;
                }
                else
                {
                    TimeTrackDb.ExtensionVersion.Value = "0.0";
                }
            }
            return ExtensionVersion.Value;
        }

        public DbSet<VideoData> WatchTimes { get; set; }
        public DbSet<RedditData> Threads { get; set; }
        public DbSet<IgnoreData> Ignores { get; set; }

        public void AddVideo(ulong user, string id, double time)
        {
            var vid = new VideoData()
            {
                UserId = user,
                VideoId = id,
                WatchedTime = time,
                LastUpdated = DateTime.Now
            };
            var existing = WatchTimes.Find(vid._userId, vid.VideoId);
            if (existing == null)
            {
                WatchTimes.Add(vid);
            }
            else
            {
                existing.LastUpdated = DateTime.Now;
                existing.WatchedTime = time;
                WatchTimes.Update(existing);
            }
        }
        public void AddThread(ulong user, string id, int comments)
        {
            var rtd = new RedditData()
            {
                UserId = user,
                ThreadId = id,
                Comments = comments,
                LastUpdated = DateTime.Now
            };
            Threads.Add(rtd);
        }
        public void AddIgnored(ulong user, string id, bool isIgnored)
        {
            var ignore = new IgnoreData()
            {
                UserId = user,
                VideoId = id
            };
            if (!isIgnored)
            {

                // we're removing it
                var existing = Ignores.Find(ignore._userId, ignore.VideoId);
                if (existing == null)
                    return;
                Ignores.Remove(existing);
                return;
            }
            Ignores.Add(ignore);
        }


        public VideoData GetVideo(ulong user, string video)
        {
            unchecked
            {
                var v = WatchTimes.FirstOrDefault(x => x._userId == (long)user && x.VideoId == video);
                return v;
            }
        }
        public RedditData[] GetThread(ulong user, string thread)
        {
            unchecked
            {
                var v = Threads
                    .AsAsyncEnumerable()
                    .Where(x => x._userId == (long)user && x.ThreadId == thread)
                    .OrderBy(x => x.LastUpdated)
                    .ToArrayAsync();
                return v.Result;
            }
        }
        public IgnoreData[] GetIgnoreDatas(ulong user)
        {
            unchecked
            {
                var x = Ignores.AsAsyncEnumerable().Where(x => x._userId == (long)user).ToArrayAsync().Result;
                return x;
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasCharSet("utf8mb4");

            modelBuilder.Entity<VideoData>()
                .HasKey(x => new { x._userId, x.VideoId });
            modelBuilder.Entity<RedditData>()
                .HasKey(x => new { x._userId, x.ThreadId, x.LastUpdated });
            modelBuilder.Entity<IgnoreData>()
                .HasKey(x => new { x._userId, x.VideoId });

        }
    }

    public class IgnoreData
    {
        public long _userId
        {
            get
            {
                unchecked { return (long)UserId; }
            }
            set
            {
                unchecked { UserId = (ulong)value; }
            }
        }

        [NotMapped]
        public ulong UserId { get; set; }

        public string VideoId { get; set; }
    }

    public class VideoData
    {
        public long _userId
        {
            get
            {
                unchecked { return (long)UserId; }
            }
            set
            {
                unchecked { UserId = (ulong)value; }
            }
        }

        [NotMapped]
        public ulong UserId { get; set; }

        public string VideoId { get; set; }

        public double WatchedTime { get; set; }

        public DateTime LastUpdated { get; set; }
    }

    public class RedditData
    {
        public long _userId
        {
            get
            {
                unchecked { return (long)UserId; }
            }
            set
            {
                unchecked { UserId = (ulong)value; }
            }
        }

        [NotMapped]
        public ulong UserId { get; set; }

        public string ThreadId { get; set; }

        public int Comments { get; set; }

        public DateTime LastUpdated { get; set; }
    }

}
