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
        public override void OnReady(IServiceProvider services)
        {
#if DEBUG
            return;
#endif
            using var db = Program.GlobalServices.GetRequiredService<TimeTrackDb>();
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


    public class TimeTrackDb : DbContext
    {
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
                var client = Program.GlobalServices.GetRequiredService<Classes.BotHttpClient>();
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
            unchecked
            {
                var v = Threads
                    .AsAsyncEnumerable()
                    .Where(x => x.UserId == user && x.ThreadId == thread)
                    .OrderBy(x => x.LastUpdated)
                    .ToArrayAsync();
                return v.Result;
            }
        }
        public IgnoreData[] GetIgnoreDatas(uint user)
        {
            unchecked
            {
                var x = Ignores.AsAsyncEnumerable().Where(x => x.UserId == user).ToArrayAsync().Result;
                return x;
            }
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

    public class IgnoreData
    {
        public uint UserId { get; set; }
        public string VideoId { get; set; }
    }

    public class VideoData
    {
        public uint UserId { get; set; }

        public string VideoId { get; set; }

        public double WatchedTime { get; set; }

        public DateTime LastUpdated { get; set; }
    }

    public class RedditData
    {
        public uint UserId { get; set; }

        public string ThreadId { get; set; }

        public int Comments { get; set; }

        public DateTime LastUpdated { get; set; }
    }

}
