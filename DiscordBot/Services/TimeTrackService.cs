using DiscordBot.Websockets;
using Microsoft.EntityFrameworkCore.Design;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using DiscordBot.Utils;
using DiscordBot.Classes.DbContexts;

namespace DiscordBot.Services
{
    public class TimeTrackService : Service
    {
        public override void OnReady(IServiceProvider services)
        {
#if DEBUG
            return;
#endif
            using var db = Program.GlobalServices.GetTimeDb("OnReady");
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
