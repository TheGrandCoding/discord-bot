using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiscordBot.Utils;

namespace DiscordBot.Classes.Calender
{
    public class CalenderFactory : IDesignTimeDbContextFactory<CalenderDb>
    {
        public CalenderDb CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<CalenderDb>();
            builder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=calendar;MultipleActiveResultSets=true");
            builder.EnableSensitiveDataLogging();
            return new CalenderDb(builder.Options);
        }
    }
    public class CalenderDb : DbContext
    {
        public CalenderDb([NotNullAttribute] DbContextOptions options) : base(options)
        {
        }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.WithSQLConnection("calendar");
        }

        public static Semaphore Lock = new Semaphore(1, 1);


        public DbSet<CalenderEvent> Events { get; set; }
        public DbSet<Attendee> Attendees { get; set; }
        public DbSet<CalenderSeries> Series { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasCharSet("utf8mb4");

            modelBuilder.Entity<Attendee>()
                .HasKey(x => new { x.EventId, x._userId });

            modelBuilder.Entity<CalenderEvent>()
                .HasMany(x => x.Attendees)
                .WithOne(x => x.Event)
                .HasForeignKey(x => x.EventId);
            modelBuilder.Entity<CalenderEvent>()
                .Navigation(x => x.Attendees)
                .AutoInclude()
                .UsePropertyAccessMode(PropertyAccessMode.Property);

            modelBuilder.Entity<CalenderSeries>()
                .HasMany(x => x.Events)
                .WithOne(x => x.Series)
                .HasForeignKey(x => x.SeriesId);
            modelBuilder.Entity<CalenderSeries>()
                .Navigation(x => x.Events)
                .AutoInclude()
                .UsePropertyAccessMode(PropertyAccessMode.Property);
        }

        public CalenderEvent AddEvent(Action<CalenderEvent> action, bool doSave = true)
        {

            var evnt = new CalenderEvent();
            action(evnt);

            Events.Add(evnt);
            if (doSave)
                SaveChanges();

            return evnt;

            /*var ls = new List<CalenderEvent>();
            DateTime finishDate;
            do
            {
                finishDate = evnt.Start.Add(evnt.Duration);
                if(finishDate.DayOfYear == evnt.Start.DayOfYear)
                {
                    ls.Add(evnt);
                } else
                {
                    var endOfDay = evnt.Start.Date.Add(new TimeSpan(23, 59, 59, 59, 999));
                    var duration = endOfDay - evnt.Start;
                    var firstEvent = evnt.Clone();
                    firstEvent.Start = evnt.Start;
                    firstEvent.End = firstEvent.Start.Add(duration);

                    var secondEvent = evnt.Clone();
                    secondEvent.Start = evnt.Start.Date.AddDays(1); // next day
                    var remainder = evnt.Duration - duration;
                    secondEvent.End = secondEvent.Start.Add(remainder);

                    ls.Add(firstEvent);
                    ls.Add(secondEvent);
                }

            } while(finishDate.DayOfYear != evnt.Start.DayOfYear || finishDate.Year != evnt.Start.Year);
            if(ls.Count > 1)
            {
                for(int i = 0; i < ls.Count; i++)
                {
                    var item = ls[i];
                    item.Name = item.Name + $" ({i + 1}/{ls.Count})";
                }
            }
            Events.AddRange(ls);
            if (doSave)
                SaveChanges();
            return ls;*/
        }

        async Task<List<CalenderEvent>> getSingleEventsBetween(DateTime start, DateTime end)
        {
            var events = await Events.AsAsyncEnumerable()
                .Where(x => 
                        ((x.Start >= start && x.Start < end) || (x.End >= start && x.End < end)) // either side within range
                    ||  (x.Start < start && x.End > end) // event straddles range
                )
                .ToListAsync();
            return events;
        }

        async Task<List<CalenderEvent>> getNewRecurrenceEventsBetween(DateTime start, DateTime end)
        {
            var manySeri = await Series.AsAsyncEnumerable()
                .Where(x => ((x.StartRecur >= start && x.StartRecur < end) || (x.EndRecur >= start && x.EndRecur < end))
                    || (x.StartRecur <= start && x.EndRecur >= end)
                )
                .ToListAsync();
            var events = new List<CalenderEvent>();

            foreach(var series in manySeri)
            {
                var existingEvents = series.Events;
                var template = existingEvents.FirstOrDefault();
                var recursOn = series.RecursOn.Split(",").Select(x => (DayOfWeek)int.Parse(x)).ToArray();
                if(template == null)
                {
                    Program.LogWarning($"Series {series.Id} has no events to use as a template", "Calendar");
                    continue;
                }
                var firstDate = start.Date;
                while(firstDate < end.Date && firstDate < series.EndRecur)
                {
                    if(recursOn.Contains(firstDate.DayOfWeek))
                    {
                        var existing = existingEvents.FirstOrDefault(x => x.Start.Date == firstDate);
                        if(existing == null)
                        {
                            var startTime = firstDate.Add(template.Start.TimeOfDay);
                            existing = new CalenderEvent()
                            {
                                CreatedById = template.CreatedById,
                                Start = startTime,
                                End = startTime.Add(template.Duration),
                                Name = template.Name,
                                Priority = template.Priority,
                                Visibility = template.Visibility,
                                Series = series,
                                SeriesId = series.Id,
                            };
                            if(series.IsValidEvent(existing)) 
                                events.Add(existing);
                        }
                    }
                    firstDate = firstDate.AddDays(1);
                }
            }

            if(events.Count > 0)
            {
                Events.AddRange(events);
                SaveChanges();
            }
            return events;
        }

        public async Task<List<CalenderEvent>> GetEventsBetween(DateTime start, DateTime end)
        {
            var events = await getSingleEventsBetween(start, end);
            events.AddRange(await getNewRecurrenceEventsBetween(start, end));
            return events;
        }

#if DEBUG
        public void AddDefaultEvents()
        {
            Events.ToList().ForEach(x => Events.Remove(x));
            Series.ToList().ForEach(x => Series.Remove(x));
            SaveChanges();

            var sleepSeries = new CalenderSeries()
            {
                RecursOn = "0,1,2,3,4",
                StartRecur = DateTime.UtcNow.Date.AddDays(-7),
                EndRecur = DateTime.UtcNow.Date.AddDays(7)
            };
            Series.Add(sleepSeries);
            SaveChanges();


            var monday = DateTime.Now.Date;
            while (monday.DayOfWeek != DayOfWeek.Monday)
                monday = monday.AddDays(-1);
            uint id = 1;
            var sleep = AddEvent(x =>
            {
                x.Name = "Sleep";
                x.Priority = CalenderPriority.Illegal;
                x.Start = monday.AddHours(22).ToUniversalTime();
                x.End = monday.AddHours(22 + 9).ToUniversalTime();
                x.Visibility = EventVisibility.Full;
                x.Attendees = new List<Attendee>();
                x.CreatedById = id;
                x.Series = sleepSeries;
                x.SeriesId = sleepSeries.Id;
            }, doSave: false);

            sleepSeries.Events.Add(sleep);

#if remove
            var work = AddEvent(x =>
            {
                x.Name = "Work";
                x.Priority = CalenderPriority.Illegal;
                x.Start = monday.AddHours(8).ToUniversalTime();
                x.End = monday.AddHours(8 + 9).ToUniversalTime();
                x.Public = true;
                x.CreatedById = id;
            }, doSave: false);
            events.Add(work);
            monday = monday.AddDays(5);
            // sunday by now
            var otherUser = AddEvent(x =>
            {
                x.Name = "Hangout";
                x.Priority = CalenderPriority.Disliked;
                x.Start = monday.AddHours(10).ToUniversalTime();
                x.End = monday.AddHours(10 + 3).ToUniversalTime();
                x.Public = true;
                x.CreatedById = 753669933635993711;
            });
            SaveChanges();
            var attn = new Attendee(otherUser.Id, id);
            Attendees.Add(attn);
            otherUser.Attendees.Add(attn);
#endif
            SaveChanges();
        }
#endif
        }

    public class CalenderEvent
    {
        public CalenderEvent()
        {
        }
        private CalenderEvent(CalenderDb db, int id, DateTime start, DateTime end)
        {
            Id = id;
            Start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            End = DateTime.SpecifyKind(end, DateTimeKind.Utc);
        }
        
        public int Id { get; set; }

        public uint CreatedById { get; set; }

        public string Name { get; set; }

        public CalenderPriority Priority { get; set; }

        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        [NotMapped]
        public TimeSpan Duration => End - Start;


        public EventVisibility Visibility { get; set; }

        public List<Attendee> Attendees { get; set; } = new List<Attendee>();
        
        public int? SeriesId { get; set; }
        public CalenderSeries Series { get; set; }


        string priorityColor {  get
            {
                switch(this.Priority)
                {
                    case CalenderPriority.Illegal:
                        return "black";
                    case CalenderPriority.Disliked:
                        return "grey";
                    case CalenderPriority.Preferred:
                        return "blue";
                    case CalenderPriority.StronglyPreferred:
                        return "green";
                    default:
                        return "white";
                }
            } }

        int getCount(string x, Func<int, bool> act)
        {
            int i = 0;
            foreach(var c in x)
            {
                var p = int.Parse(c.ToString());
                if (act(p))
                    i++;
            }
            return i;
        } 

        string userColor {  get
            {
                var id = CreatedById.ToString();
                int r = (int)(getCount(id, i => i >= 0 && i <= 3) / 10d) * 255;
                int g = (int)(getCount(id, i => i > 3 && i <= 6) / 10d) * 255;
                int b = (int)(getCount(id, i => i > 9 && i <= 9) / 10d) * 255;
                return $"#{r:X2}{g:X2}{b:X2}";
            }
        }

        public bool IsVisibleTo(BotDbUser user)
        {
            if (Visibility == EventVisibility.Full)
                return true;
            if (Visibility == EventVisibility.AvailabilityOnly)
                return true;

            return DoesAttend(user);
        }

        public bool CanSeeInfo(BotDbUser user)
        {
            if (Visibility == EventVisibility.Full)
                return true;

            return DoesAttend(user);
        }

        public bool DoesAttend(BotDbUser user)
        {
            return CreatedById == user.Id || (Attendees ?? new List<Attendee>()).Any(x => x?.UserId == user.Id);
        }

        public JObject ToJson(BotDbUser user)
        {
            bool seeinfo = CanSeeInfo(user);
            var jobj = new JObject();
            jobj["id"] = Id;
            var start = new DateTimeOffset(Start.Ticks, TimeSpan.Zero);
            var end = new DateTimeOffset(End.Ticks, TimeSpan.Zero);
            jobj["start"] = start.ToUnixTimeMilliseconds();
            jobj["end"] = end.ToUnixTimeMilliseconds();
            jobj["title"] = seeinfo ? Name : "";
            var extended = new JObject();
            extended["visibility"] = (int)Visibility;
            if(seeinfo && Series != null)
            {
                var seriesInfo = new JObject();
                seriesInfo["id"] = SeriesId;
                seriesInfo["daysOfWeek"] = new JArray(Series.RecursOn.Split(",").Select(x => int.Parse(x)).ToList());
                seriesInfo["startRecur"] = new DateTimeOffset(Series.StartRecur, TimeSpan.Zero).ToUnixTimeMilliseconds();
                seriesInfo["endRecur"] = new DateTimeOffset(Series.EndRecur, TimeSpan.Zero).ToUnixTimeMilliseconds();
                extended["series"] = seriesInfo;

                jobj["groupId"] = SeriesId;
                jobj["eventStartEditable"] = false; // prevent dragging
                jobj["eventDurationEditable"] = user.Id == CreatedById;
            } else
            {
                jobj["editable"] = user.Id == CreatedById;
            }

            jobj["backgroundColor"] = userColor;
            jobj["borderColor"] = priorityColor;

            if (seeinfo == false || Name.Contains("sleep", StringComparison.OrdinalIgnoreCase))
                jobj["display"] = "background";

            extended["creator"] = CreatedById.ToString();
            var attending = new JArray();
            if(seeinfo)
            {
                foreach (var attendee in (Attendees ?? new List<Attendee>()))
                {
                    attending.Add(attendee.UserId.ToString());
                }
            }
            extended["attendees"] = attending;

            if(Priority == CalenderPriority.Illegal)
            {
                extended["priority"] = "illegal";
            } else
            {
                extended["priority"] = (int)Priority;
            }

            jobj["extendedProps"] = extended;

            return jobj;
        }
    }

    public class Attendee
    {
        public Attendee(int eventId, ulong userId)
        {
            EventId = eventId;
            UserId = userId;
        }
        private Attendee(CalenderDb db, int eventId)
        {
            EventId = eventId;
        }

        public int EventId { get; set; }
        [ForeignKey("EventId")]
        public CalenderEvent Event { get; set; }

        [NotMapped]
        public ulong UserId { get; set; }

        [Column("UserId")]
        public long _userId { get => (long)UserId; set => UserId = (ulong)value; }
    }

    public class CalenderSeries
    {
        public CalenderSeries()
        {
        }
        private CalenderSeries(CalenderDb db, DateTime startRecur, DateTime endRecur)
        {
            StartRecur = DateTime.SpecifyKind(startRecur, DateTimeKind.Utc);
            EndRecur = DateTime.SpecifyKind(endRecur, DateTimeKind.Utc);
        }

        public int Id { get; set; }

        public string RecursOn { get; set; }
        public DateTime StartRecur { get; set; }
        public DateTime EndRecur { get; set; }

        public bool IsValidEvent(CalenderEvent evnt) 
        {
            if (evnt.SeriesId != Id)
                return false;
            if (evnt.Start < StartRecur)
                return false;
            if (evnt.Start > EndRecur)
                return false;
            if (evnt.End > EndRecur)
                return false;
            var recurs = RecursOn.Split(",").Select(x => int.Parse(x));
            if (!recurs.Contains((int)evnt.Start.DayOfWeek))
                return false;
            return true;
        }

        public List<CalenderEvent> Events { get; set; } = new List<CalenderEvent>();
    }

    public enum CalenderPriority
    {
        Illegal = int.MinValue,
        Disliked = -1,
        Neutral = 0,
        Preferred = 1,
        StronglyPreferred = 2
    }

    public enum EventVisibility
    {
        /// <summary>
        /// Event name/etc is fully visible to everyone
        /// </summary>
        Full = 0,
        /// <summary>
        /// Availability for duration is visible to everyone, but name is withheld for attendees.
        /// </summary>
        AvailabilityOnly = 1,
        /// <summary>
        /// Event information only visible for attendees.
        /// </summary>
        Private = 2
    }
}
