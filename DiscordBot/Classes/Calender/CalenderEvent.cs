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


        public DbSet<CalenderEvent> Events { get; set; }
        public DbSet<Attendee> Attendees { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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


        static bool first = false;
        public async Task<List<CalenderEvent>> GetEventsBetween(DateTime start, DateTime end)
        {
            if(!first)
            {
                first = true;
                AddDefaultEvents();
            }
            var events = await Events.AsAsyncEnumerable()
                .Where(x => (x.Start >= start && x.Start < end) || (x.End >= start && x.End < end))
                // if start is between range, OR the end is between range, then we care about it
                .ToListAsync();
            return events;
        }

#if DEBUG
        public List<CalenderEvent> AddDefaultEvents()
        {
            Events.ToList().ForEach(x => Events.Remove(x));
            SaveChanges();
            var events = new List<CalenderEvent>();
            var monday = DateTime.Now.Date;
            while (monday.DayOfWeek != DayOfWeek.Monday)
                monday = monday.AddDays(-1);
            ulong id = 144462654201790464;
            var sleep = AddEvent(x =>
            {
                x.Name = "Sleep";
                x.Priority = CalenderPriority.Illegal;
                x.Start = monday.AddHours(22).ToUniversalTime();
                x.End = monday.AddHours(22 + 9).ToUniversalTime();
                x.Public = true;
                x.Attendees = new List<Attendee>();
                x.CreatedById = id;
            }, doSave: false);
            events.Add(sleep);
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
            SaveChanges();
            return events;
        }
#endif
    }

    public class CalenderEvent
    {
        public CalenderEvent()
        {
        }
        private CalenderEvent(CalenderDb db, int id)
        {
            Id = id;
            /*var x = db.Events.FirstOrDefault(x => x.Id == id + 1);
            Attendees = db.Attendees
                .AsQueryable()
                .Where(x => x.EventId == Id)
                .ToList();*/
        }
        


        public int Id { get; set; }

        [NotMapped]
        public ulong CreatedById { get; set; }

        [Column("CreatedById")]
        public long _createdById { get => (long)CreatedById; set => CreatedById = (ulong)value; }

        public string Name { get; set; }

        public CalenderPriority Priority { get; set; }

        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        [NotMapped]
        public TimeSpan Duration => End - Start;

        public List<Attendee> Attendees { get; set; }

        public bool Public { get; set; }

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

        public JObject ToJson(BotUser user)
        {
            var jobj = new JObject();
            jobj["id"] = Id;
            var start = new DateTimeOffset(Start.Ticks, TimeSpan.Zero);
            var end = new DateTimeOffset(End.Ticks, TimeSpan.Zero);
            jobj["start"] = start.ToUnixTimeMilliseconds();
            jobj["end"] = end.ToUnixTimeMilliseconds();
            jobj["title"] = Name;
            jobj["editable"] = user.Id == CreatedById;

            jobj["backgroundColor"] = userColor;
            jobj["borderColor"] = priorityColor;

            if (Name.Contains("sleep", StringComparison.OrdinalIgnoreCase))
                jobj["display"] = "background";

            var extended = new JObject();
            extended["creator"] = CreatedById.ToString();
            var attending = new JArray();
            foreach (var attendee in Attendees)
            {
                attending.Add(attendee.UserId.ToString());
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

    public enum CalenderPriority
    {
        Illegal = int.MinValue,
        Disliked = -1,
        Neutral = 0,
        Preferred = 1,
        StronglyPreferred = 2
    }
}
