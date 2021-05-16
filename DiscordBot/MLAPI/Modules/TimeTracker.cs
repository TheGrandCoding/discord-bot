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

namespace DiscordBot.MLAPI.Modules.TimeTracking
{
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
            foreach(var id in videoIds)
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
            foreach(var videos in response.Items)
            {
                dict[videos.Id] = videos;
                VideoInfo[videos.Id] = videos;
            }
            return dict;
        }

        public static string GetExtensionVersion(bool bypassCache = false)
        {
            if(bypassCache || ExtensionVersion.GetValueOrDefault(null) == null)
            {
                var client = Program.Services.GetRequiredService<HttpClient>();
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
            var existing = Threads.Find(rtd._userId, rtd.ThreadId);
            if (existing == null)
            {
                Threads.Add(rtd);
            }
            else
            {
                existing.LastUpdated = DateTime.Now;
                existing.Comments = comments;
                Threads.Update(existing);
            }
        }


        public VideoData GetVideo(ulong user, string video)
        {
            unchecked
            {
                var v = WatchTimes.FirstOrDefault(x => x._userId == (long)user && x.VideoId == video);
                return v;
            }
        }
        public RedditData GetThread(ulong user, string thread)
        {
            unchecked
            {
                var v = Threads.FirstOrDefault(x => x._userId == (long)user && x.ThreadId == thread);
                return v;
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<VideoData>()
                .HasKey(x => new { x._userId, x.VideoId });
            modelBuilder.Entity<RedditData>()
                .HasKey(x => new { x._userId, x.ThreadId });
        }
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

    public class TimeTracker : AuthedAPIBase
    {
        public TimeTracker(APIContext context) : base(context, "tracker")
        {
            DB = Program.Services.GetRequiredService<TimeTrackDb>();
        }

        public TimeTrackDb DB { get; }

        [Method("GET"), Path("/tracker")]
        public void Base()
        {
            var existing = Context.User.Tokens.FirstOrDefault(x => x.Name == AuthToken.TimeToken);
            if(existing == null)
            {
                existing = new AuthToken(AuthToken.TimeToken, 12, "html.api.tracker", "html.api.tracker.*");
                Context.User.Tokens.Add(existing);
                Program.Save();
            }
            RespondRaw(existing.Value);
        }

        [Method("GET"), Path("/api/tracker/user")]
        [RequireAuthentication(false, false)]
        [RequireApproval(false)]
        [RequireScope("html.?")]
        public void GetUser()
        {
            JToken obj;
            if (Context.User == null)
            {
                obj = JValue.CreateNull();
            }
            else
            {
                obj = new JObject();
                obj["id"] = Context.User.Id.ToString();
                obj["name"] = Context.User.Name;
                var intervalThings = new JObject();
                intervalThings["get"] = 10_000;
                intervalThings["set"] = 15_000;
                obj["interval"] = intervalThings;
            }
            RespondRaw(obj.ToString(), HttpStatusCode.OK);
        }

        [Method("GET"), Path("/api/tracker/latestVersion")]
        public void LatestVersion()
        {
            RespondRaw(TimeTrackDb.GetExtensionVersion(), HttpStatusCode.OK);
        }

        [Method("GET"), Path("/api/tracker/times")]
        public void GetTimes(string ids)
        {
            var jobj = new JObject();
            foreach (var id in ids.Split(';', ',')) 
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                var thing = DB.GetVideo(Context.User.Id, id);
                jobj[id] = thing?.WatchedTime ?? 0d;
            }
            RespondRaw(jobj.ToString(), 200);
        }

        [Method("POST"), Path("/api/tracker/times")]
        public void SetTimes()
        {
            var jobj = JObject.Parse(Context.Body);
            foreach(JProperty token in jobj.Children())
            {
                var val = token.Value.ToObject<double>();
                DB.AddVideo(Context.User.Id, token.Name, val);
            }
            DB.SaveChanges();
            RespondRaw("OK", HttpStatusCode.Created);
        }

        [Method("GET"), Path("/api/tracker/threads")]
        public void GetThreads(string ids)
        {
            var jobj = new JObject();
            foreach (var id in ids.Split(';', ','))
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                var thing = DB.GetThread(Context.User.Id, id);
                var threadObj = new JObject();
                threadObj["time"] = new DateTimeOffset(thing.LastUpdated).ToUnixTimeMilliseconds();
                threadObj["count"] = thing.Comments;
                jobj[id] = threadObj;
            }
            RespondRaw(jobj.ToString(), 200);
        }

        [Method("POST"), Path("/tracker/webhook")]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        [RequireScope("*")]
        [RequireGithubSignatureValid("tracker:webhook")]
        public void VersionUpdate()
        {
            RespondRaw("Thanks");
            var jobj = JObject.Parse(Context.Body);
            var release = jobj["release"]["tag_name"].ToObject<string>().Substring(1);
            TimeTrackDb.SetExtVersion(release);
            if(WSService.Server.WebSocketServices.TryGetServiceHost("/time-tracker", out var host))
            {
                TimeTrackerWS.BroadcastUpdate(release, host.Sessions);
            }
        }
    }
}
