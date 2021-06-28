using DiscordBot.Classes.Calender;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace DiscordBot.MLAPI.Modules
{
    public class Calender : AuthedAPIBase
    {
        public Calender(APIContext c) : base(c, "calender") { }

        static Semaphore lck = new Semaphore(1, 1);
        public override void BeforeExecute()
        {
            if(!lck.WaitOne(1000 * 5))
            {
                RespondRaw("Failed to achieve lock", 429);
                throw new HaltExecutionException("Failed to achieve lock");
            }
        }
        public override void AfterExecute()
        {
            lck.Release();
        }

        [Method("GET"), Path("/calender")]
        public void RedirectBase()
        {
            RespondRaw(LoadRedirectFile("/calendar"), System.Net.HttpStatusCode.Found);
        }

        [Method("GET"), Path("/calendar")]
        public void Base()
        {
            InjectObjects = new List<Classes.HTMLHelpers.HTMLBase>();
            ReplyFile("base.html", 200);
        }

        [Method("GET"), Path("/api/calendar")]
        public void APIGetWeek(DateTime start, DateTime end)
        {
            using var DB = Program.Services.GetRequiredService<CalenderDb>();
            var events = DB.GetEventsBetween(start, end).Result;
            var jarray = new JArray();
            foreach (var e in events)
                jarray.Add(e.ToJson(Context.User));

            RespondRaw(jarray.ToString());
        }

        [Method("GET"), Path("/api/calendar/users")]
        public void APIGetUsers()
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
        public void APIRemvoveAttendance(int eventId)
        {
            using var db = Program.Services.GetRequiredService<CalenderDb>();

            var existing = db.Attendees.FirstOrDefault(x => x.EventId == eventId && x._userId == (long)Context.User.Id);
            if(existing != null)
            {
                db.Attendees.Remove(existing);
                db.SaveChanges();
            }
            RespondRaw("OK");
        }

        [Method("POST"), Path("/api/calendar/attendee")]
        public void APIAddAttendance(int eventId)
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
        public void APIModifyEvent(string id, string name, DateTime start, int duration, string priority, 
            bool isPublic = true,
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
                RespondRaw(LoadRedirectFile("/calendar"), 302);
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
                };
            }

            existing.Name = name;
            existing.Start = start.ToUniversalTime();
            existing.End = start.AddMinutes(duration).ToUniversalTime();
            existing.Public = isPublic;

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
            RespondRaw(LoadRedirectFile("/calendar"), 302);
        }

        [Method("POST"), Path("/api/calendar/series")]
        public void APIModifySeries(int event_id, int series_id, string recursOn, DateTime startRecur, DateTime endRecur,
            string remove = null, string disconnect = null, string add = null)
        {
            using var db = Program.Services.GetRequiredService<CalenderDb>();

            var series = db.Series.FirstOrDefault(x => x.Id == series_id);
            var evnt = db.Events.FirstOrDefault(x => x.Id == event_id);

            if (remove != null)
            {
                db.Series.Remove(series);
            } else if (disconnect != null)
            {
                evnt.Series = null;
                evnt.SeriesId = 0;
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

            db.SaveChanges();
            RespondRaw("OK");


        }


        [Method("PUT"), Path("/api/calendar/move")]
        public void APIMoveEvent(int id, long start, long end)
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
