using Discord;
using DiscordBot.Classes.DbContexts;
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

namespace DiscordBot.Classes.Calender
{

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
