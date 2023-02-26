using DiscordBot.Classes.Calender;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{
    public class Calender : AuthedAPIBase
    {
        public Calender(APIContext c) : base(c, "calender") { }

        public override Task BeforeExecute()
        {
            if(!CalenderDb.Lock.WaitOne(1000 * 5))
            {
                RespondRaw("Failed to achieve lock", 429);
                throw new HaltExecutionException("Failed to achieve lock");
            }
            return Task.CompletedTask;
        }
        public override Task AfterExecute()
        {
            CalenderDb.Lock.Release();
            return Task.CompletedTask;
        }

        [Method("GET"), Path("/calender")]
        public async Task RedirectBase()
        {
            RespondRedirect("/calendar");
        }

        [Method("GET"), Path("/calendar")]
        public async Task Base()
        {
            InjectObjects = new List<Classes.HTMLHelpers.HTMLBase>();
            ReplyFile("base.html", 200);
        }

        [Method("GET"), Path("/api/calendar")]
        public async Task APIGetWeek(DateTime start, DateTime end)
        {
            using var DB = Program.Services.GetRequiredService<CalenderDb>();
            var events = DB.GetEventsBetween(start, end).Result;
            var jarray = new JArray();
            foreach (var e in events)
            {
                if(e.IsVisibleTo(Context.User))
                    jarray.Add(e.ToJson(Context.User));
            }

            RespondRaw(jarray.ToString());
        }

        [Method("GET"), Path("/api/calendar/users")]
        public async Task APIGetUsers()
        {
            var users = new JObject();
            foreach(var usr in Program.Users)
            {
                if (usr.ServiceUser || usr.GeneratedUser)
                    continue;
                var jobj = new JObject();
                jobj["username"] = usr.Username ?? usr.Name;
                jobj["discriminator"] = usr.Discriminator;
                jobj["avatar"] = usr.GetAnyAvatarUrl();
                users[$"{usr.Id}"] = jobj;
            }
            RespondRaw(users.ToString());
        }

        [Method("DELETE"), Path("/api/calendar/attendee")]
        public async Task APIRemvoveAttendance(int eventId, ulong? userId = null)
        {
            using var db = Program.Services.GetRequiredService<CalenderDb>();


            if(userId.HasValue)
            {
                if(userId.Value != Context.User.Id)
                {
                    var evnt = db.Events.FirstOrDefault(x => x.Id == eventId);
                    if(evnt.CreatedById != Context.User.Id)
                    {
                        RespondRaw("Forbidden", System.Net.HttpStatusCode.Forbidden);
                        return;
                    }
                }
            }
            var existing = db.Attendees.FirstOrDefault(x => x.EventId == eventId && x._userId == (long)userId.GetValueOrDefault(Context.User.Id));
            if(existing != null)
            {
                db.Attendees.Remove(existing);
                db.SaveChanges();
            }
            RespondRaw("OK");
        }

        [Method("POST"), Path("/api/calendar/attendee")]
        public async Task APIAddAttendance(int eventId)
        {
            using var db = Program.Services.GetRequiredService<CalenderDb>();
            var existing = db.Attendees.FirstOrDefault(x => x.EventId == eventId && x._userId == (long)Context.User.Id);
            if (existing == null)
            {
                existing = new Attendee(eventId, Context.User.Id);
                db.Attendees.Add(existing);
                db.SaveChanges();
            }
            RespondRaw("OK");
        }

        [Method("POST"), Path("/api/calendar/events")]
        public async Task APIModifyEvent(string id, string name, DateTime start, int duration, string priority, 
            EventVisibility? visibility = null,
            string remove = null, string submit = null)
        {
            using var DB = Program.Services.GetRequiredService<CalenderDb>();
            int.TryParse(id, out var idInt);
            CalenderEvent existing = string.IsNullOrWhiteSpace(id) ? null : DB.Events.Find(idInt);
            if(remove != null)
            {
                // we're deleting it
                DB.Events.Remove(existing);
                DB.SaveChanges();
                RespondRedirect("/calendar");
                return;
            }

            start = DateTime.SpecifyKind(start, DateTimeKind.Local);
            bool adding = false;
            if(existing == null)
            {   // we're adding
                adding = true;
                existing = new CalenderEvent()
                {
                    CreatedById = Context.User.Id,
                    Series = null,
                    SeriesId = null
                };
            }

            existing.Name = name;
            existing.Start = start.ToUniversalTime();
            existing.End = start.AddMinutes(duration).ToUniversalTime();
            existing.Visibility = visibility.GetValueOrDefault(existing.Visibility);

            if(priority == "illegal")
            {
                existing.Priority = CalenderPriority.Illegal;
            } else
            {
                existing.Priority = (CalenderPriority)(int.Parse(priority));
            }


            if (adding)
                DB.Events.Add(existing);
            else
                DB.Events.Update(existing);

            DB.SaveChanges();
            RespondRedirect("/calendar");
        }

        [Method("POST"), Path("/api/calendar/series")]
        public async Task APIModifySeries(int event_id, string recursOn, DateTime startRecur, DateTime endRecur,
            string series_id,
            string remove = null, string disconnect = null, string add = null)
        {
            using var db = Program.Services.GetRequiredService<CalenderDb>();

            CalenderSeries series = null;
            if(int.TryParse(series_id, out var seriesId))
            {
                series = db.Series.FirstOrDefault(x => x.Id == seriesId);
            }
            var evnt = db.Events.FirstOrDefault(x => x.Id == event_id);

            if (remove != null)
            {
                db.Series.Remove(series);
            } else if (disconnect != null)
            {
                evnt.Series = null;
                evnt.SeriesId = null;
                db.Events.Update(evnt);
            } else
            {
                bool adding = false;
                if(series == null)
                {
                    adding = true;
                    series = new CalenderSeries();
                }
                series.RecursOn = recursOn;
                series.StartRecur = startRecur.ToUniversalTime();
                series.EndRecur = endRecur.ToUniversalTime();

                evnt.Series = series;
                if (adding)
                {
                    db.Series.Add(series);
                    db.SaveChanges();
                } else
                {
                    db.Series.Update(series);
                }
                evnt.SeriesId = series.Id;
            }

            // We must now apply this change to any events within the series.

            foreach(var sEvent in series.Events)
            {
                if(remove != null)
                { // purge events
                    db.Events.Remove(sEvent);
                } else if (disconnect == null)
                { // ensure event is still valid within series.
                    // e.g. series no longer recurs on Monday, ensure event isn't one that recurs on monday.
                    if(series.IsValidEvent(sEvent) == false)
                    {
                        db.Events.Remove(sEvent);
                    }
                    // it doesn't really matter if we remove an event too zealously - it'll just be re-added later.
                    // the issue is if we *don't* remove an event, then it'll be out of sync.
                }
            }

            db.SaveChanges();
            RespondRedirect("/calendar");


        }


        [Method("PUT"), Path("/api/calendar/move")]
        public async Task APIMoveEvent(int id, long start, long end)
        {
            using var DB = Program.Services.GetRequiredService<CalenderDb>();
            var startDate = DateTimeOffset.FromUnixTimeMilliseconds(start);
            var endDate = DateTimeOffset.FromUnixTimeMilliseconds(end);
            var evnt = DB.Events.Find(id);
            if(evnt == null)
            {
                RespondRaw("Event not found", 404);
                return;
            }
            if(evnt.CreatedById != Context.User.Id)
            {
                RespondRaw("Forbidden - you did not create this event", 403);
                return;
            }
            evnt.Start = startDate.UtcDateTime;
            evnt.End = endDate.UtcDateTime;
            DB.Events.Update(evnt);
            DB.SaveChanges();
            RespondRaw("OK", 200);
        }
    }
}
