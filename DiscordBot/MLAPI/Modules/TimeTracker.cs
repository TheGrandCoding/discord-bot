using DiscordBot.Classes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Net;
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

        public DbSet<VideoData> WatchTimes { get; set; }

        public void Add(ulong user, string id, double time)
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

        public VideoData Get(ulong user, string video)
        {
            unchecked
            {
                var v = WatchTimes.FirstOrDefault(x => x._userId == (long)user && x.VideoId == video);
                return v;
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<VideoData>()
                .HasKey(x => new { x._userId, x.VideoId });
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

        [Method("GET"), Path("/api/tracker/times")]
        public void GetTimes(string ids)
        {
            var jobj = new JObject();
            foreach (var id in ids.Split(';', ',')) 
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                var thing = DB.Get(Context.User.Id, id);
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
                DB.Add(Context.User.Id, token.Name, val);
            }
            DB.SaveChanges();
            RespondRaw("OK", HttpStatusCode.Created);
        }

        [Method("POST"), Path("/api/tracker/log")]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        [RequireScope("*")]
        public void Log()
        {
            var jobj = JObject.Parse(Context.Body);
            Program.LogMsg(jobj["message"].ToObject<string>(), Discord.LogSeverity.Info, "TimeTracker");
        }

    }
}
