using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Classes.Calender
{
    public class CalenderDb : DbContext
    {
        public DbSet<CalenderEvent> Events { get; set; }
        public DbSet<Attendee> Attendees { get; set; }

        public List<CalenderEvent> AddEvent(Action<CalenderEvent> action, bool doSave = true)
        {
            var evnt = new CalenderEvent();
            action(evnt);
            var ls = new List<CalenderEvent>();
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
                    firstEvent.Duration = duration;

                    var secondEvent = evnt.Clone();
                    secondEvent.Start = evnt.Start.Date.AddDays(1); // next day
                    var remainder = evnt.Duration - duration;
                    secondEvent.Duration = remainder;

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
            return ls;
        }


        public async Task<List<CalenderEvent>> GetEventsBetween(DateTime start, DateTime end)
        {
            var events = await Events.AsAsyncEnumerable()
                .Where(x => x.Start >= start && (x.Start.Add(x.Duration)) < end)
                .ToListAsync();
            return events;
        }

#if DEBUG
        public List<CalenderEvent> AddDefaultEvents()
        {
            var events = new List<CalenderEvent>();
            var monday = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
            while (monday.DayOfWeek != DayOfWeek.Monday)
                monday = monday.AddDays(-1);
            int id = 0;
            for (int i = 0; i < 6; i++)
            {
                var sleep = AddEvent(x =>
                {
                    x.Name = "Sleep";
                    x.Priority = CalenderPriority.Illegal;
                    x.Duration = TimeSpan.FromHours(8);
                    x.Start = monday.AddHours(20);
                    x.Public = true;
                    x.Attendees = new List<Attendee>();
                });
                events.AddRange(sleep);
                if (i <= 4)
                {
                    var work = AddEvent(x =>
                    {
                        x.Name = "Work";
                        x.Priority = CalenderPriority.Illegal;
                        x.Duration = TimeSpan.FromHours(10);
                        x.Start = monday.AddHours(7.5);
                        x.Public = true;
                    });
                    events.AddRange(work);
                }
            }
            return events;
        }
#endif
    }

    public class CalenderEvent
    {
        public int Id { get; set; }

        [NotMapped]
        public ulong CreatedById { get; set; }

        [Column("CreatedById")]
        long _createdById { get => (long)CreatedById; set => CreatedById = (ulong)value; }

        public string Name { get; set; }

        public CalenderPriority Priority { get; set; }

        public DateTime Start { get; set; }

        public TimeSpan Duration { get; set; }

        public List<Attendee> Attendees { get; set; } = new List<Attendee>();

        public bool Public { get; set; }

        public CalenderEvent Clone()
        {
            var clone = new CalenderEvent();
            clone.CreatedById = this.CreatedById;
            clone.Name = this.Name;
            clone.Priority = this.Priority;
            clone.Start = this.Start;
            clone.Duration = this.Duration;
            clone.Attendees = this.Attendees;
            clone.Public = this.Public;
            return clone;
        }

        public JObject ToJson()
        {
            var jobj = new JObject();
            jobj["id"] = Id;
            jobj["start"] = new DateTimeOffset(Start).ToUnixTimeMilliseconds();
            jobj["end"] = new DateTimeOffset(Start.Add(Duration)).ToUnixTimeMilliseconds();
            jobj["title"] = Name;
            if (Priority == CalenderPriority.Illegal)
                jobj["backgroundColor"] = "black";
            return jobj;
        }
    }

    public class Attendee
    {
        public CalenderEvent Event { get; set; }
        [NotMapped]
        public ulong UserId { get; set; }

        [Column("UserId")]
        long _userId { get => (long)UserId; set => UserId = (ulong)value; }
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
