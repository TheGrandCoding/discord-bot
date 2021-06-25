using DiscordBot.Classes.Calender;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    public class Calender : AuthedAPIBase
    {
        public Calender(APIContext c) : base(c, "calender") { }

        [Method("GET"), Path("/calender")]
        public void RedirectBase() => LoadRedirectFile("/calendar");

        [Method("GET"), Path("/calendar")]
        public void Base()
        {
            ReplyFile("base.html", 200);
        }

        [Method("GET"), Path("/api/calendar/")]
        public void APIGetWeek(DateTime start, DateTime end)
        {
            using var DB = Program.Services.GetRequiredService<CalenderDb>();
            var events = DB.GetEventsBetween(start, end).Result;
            var jarray = new JArray();
            foreach (var e in events)
                jarray.Add(e.ToJson());

            RespondRaw(jarray.ToString());
            
        }
    }
}
