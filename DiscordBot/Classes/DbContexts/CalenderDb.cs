using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscordBot.Utils;
using DiscordBot.Classes.Calender;

namespace DiscordBot.Classes.DbContexts
{
    public class CalenderDb : AbstractDbBase
    {
        private static int _count = 0;
        public static SemaphoreSlim _semaphore = new(1, 1);
        protected override int _lockCount { get => _count; set => _count = value; }
        protected override SemaphoreSlim _lock => _semaphore;
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.WithSQLConnection("calendar");
        }


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
                        x.Start >= start && x.Start < end || x.End >= start && x.End < end // either side within range
                    || x.Start < start && x.End > end // event straddles range
                )
                .ToListAsync();
            return events;
        }

        async Task<List<CalenderEvent>> getNewRecurrenceEventsBetween(DateTime start, DateTime end)
        {
            var manySeri = await Series.AsAsyncEnumerable()
                .Where(x => x.StartRecur >= start && x.StartRecur < end || x.EndRecur >= start && x.EndRecur < end
                    || x.StartRecur <= start && x.EndRecur >= end
                )
                .ToListAsync();
            var events = new List<CalenderEvent>();

            foreach (var series in manySeri)
            {
                var existingEvents = series.Events;
                var template = existingEvents.FirstOrDefault();
                var recursOn = series.RecursOn.Split(",").Select(x => (DayOfWeek)int.Parse(x)).ToArray();
                if (template == null)
                {
                    Program.LogWarning($"Series {series.Id} has no events to use as a template", "Calendar");
                    continue;
                }
                var firstDate = start.Date;
                while (firstDate < end.Date && firstDate < series.EndRecur)
                {
                    if (recursOn.Contains(firstDate.DayOfWeek))
                    {
                        var existing = existingEvents.FirstOrDefault(x => x.Start.Date == firstDate);
                        if (existing == null)
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
                            if (series.IsValidEvent(existing))
                                events.Add(existing);
                        }
                    }
                    firstDate = firstDate.AddDays(1);
                }
            }

            if (events.Count > 0)
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
}
