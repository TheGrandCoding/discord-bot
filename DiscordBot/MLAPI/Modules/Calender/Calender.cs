using DiscordBot.Classes.Calender;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    public class Calender : AuthedAPIBase
    {
        public Calender(APIContext c) : base(c, "calender") { }


        [Method("GET"), Path("/calender")]
        public void Base()
        {
            ReplyFile("base.html", 200);
        }

        [Method("GET"), Path("/api/calender/")]
        public void APIGetWeek(int unix)
        {
            using var DB = Program.Services.GetRequiredService<CalenderDb>();
        }
    }
}
