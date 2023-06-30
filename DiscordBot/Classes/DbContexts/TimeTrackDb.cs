using DiscordBot.Classes;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using DiscordBot.Utils;
using System.Threading;
using DiscordBot.Services;

namespace DiscordBot.Classes.DbContexts
{
    public class TimeTrackDb : AbstractDbBase
    {
        private static int _count = 0;
        private static SemaphoreSlim _semaphore = new(1, 1);
        protected override int _lockCount { get => _count; set => _count = value; }
        protected override SemaphoreSlim _lock => _semaphore;

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.WithSQLConnection("watch", true);
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
            var client = Program.GlobalServices.GetRequiredService<YouTubeService>();
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
                var client = Program.GlobalServices.GetRequiredService<BotHttpClient>();
                var r = client.GetAsync("https://api.github.com/repos/CheAle14/time-tracker/releases/latest").Result;
                if (r.IsSuccessStatusCode)
                {
                    var jobj = Newtonsoft.Json.Linq.JObject.Parse(r.Content.ReadAsStringAsync().Result);
                    var s = jobj["tag_name"].ToObject<string>();
                    if (s.StartsWith("v"))
                        s = s[1..];
                    ExtensionVersion.Value = s;
                }
                else
                {
                    ExtensionVersion.Value = "0.0";
                }
            }
            return ExtensionVersion.Value;
        }

        public DbSet<VideoData> WatchTimes { get; set; }
        public DbSet<RedditData> Threads { get; set; }
        public DbSet<IgnoreData> Ignores { get; set; }

        public void AddVideo(uint user, string id, double time)
        {
            var vid = new VideoData()
            {
                UserId = user,
                VideoId = id,
                WatchedTime = time,
                LastUpdated = DateTime.Now
            };
            var existing = WatchTimes.Find(vid.UserId, vid.VideoId);
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
        public void AddThread(uint user, string id, int comments)
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
        public void AddIgnored(uint user, string id, bool isIgnored)
        {
            var ignore = new IgnoreData()
            {
                UserId = user,
                VideoId = id
            };
            if (!isIgnored)
            {

                // we're removing it
                var existing = Ignores.Find(ignore.UserId, ignore.VideoId);
                if (existing == null)
                    return;
                Ignores.Remove(existing);
                return;
            }
            Ignores.Add(ignore);
        }


        public VideoData GetVideo(uint user, string video)
        {
            var v = WatchTimes.FirstOrDefault(x => x.UserId == user && x.VideoId == video);
            return v;
        }
        public RedditData[] GetThread(uint user, string thread)
        {
            return Threads.Where(x => x.UserId == user && x.ThreadId == thread)
                .OrderBy(x => x.LastUpdated)
                .ToArray();
        }
        public IgnoreData[] GetIgnoreDatas(uint user)
        {
            return Ignores.Where(x => x.UserId == user).ToArray();
        }
        public VideoData[] GetRecentVideo(uint user, DateTime? since = null)
        {
            since ??= DateTime.UtcNow.AddHours(-12);
            return WatchTimes.Where(x => x.UserId == user && x.LastUpdated >= since).ToArray();
        }
        public RedditData[] GetRecentThread(uint user, DateTime? since = null)
        {
            since ??= DateTime.UtcNow.AddHours(-12);
            var recentThreads = Threads.Where(x => x.UserId == user && x.LastUpdated >= since).DistinctBy(x => x.ThreadId).ToArray();
            var allData = new List<RedditData>();
            foreach (var thread in recentThreads)
            {
                var data = Threads.Where(x => x.UserId == user && x.ThreadId == thread.ThreadId).ToArray();
                allData.AddRange(data);
            }
            return allData.ToArray();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasCharSet("utf8mb4");

            modelBuilder.Entity<VideoData>()
                .HasKey(x => new { x.UserId, x.VideoId });
            modelBuilder.Entity<RedditData>()
                .HasKey(x => new { x.UserId, x.ThreadId, x.LastUpdated });
            modelBuilder.Entity<IgnoreData>()
                .HasKey(x => new { x.UserId, x.VideoId });

        }
    }

}
