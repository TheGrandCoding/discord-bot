using DiscordBot.Classes;
using DiscordBot.Classes.HTMLHelpers.Objects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{
    [RequireAuthentication(false)]
    public class TestThing : AuthedAPIBase
    {
        public TestThing(APIContext c) : base(c, "_other") { }

        [Method("POST"), Path("/testthing")]
        public async Task Thing(string location, int people, string start, string end)
        {
            if(!DateTime.TryParseExact(start, "yyyy-MM-dd", CultureInfo.CurrentCulture, DateTimeStyles.None, out var startDate))
            {
                await RespondRaw("Could not parse start date", 400);
                return;
            }
            if (!DateTime.TryParseExact(end, "yyyy-MM-dd", CultureInfo.CurrentCulture, DateTimeStyles.None, out var endDate))
            {
                await RespondRaw("Could not parse end date", 400);
                return;
            }
            var diff = endDate - startDate;
            var hours = Math.Abs(diff.TotalHours);
            var price = Math.Round(hours * people, 2);
            await ReplyFile("testthing.html", 200,
                new Replacements()
                .Add("location", location)
                .Add("duration", $"{diff.TotalDays:00} day(s)")
                .Add("price", $"£{price:000.00}"));
        }

        [Method("PUT"), Path("/testthing")]
        public async Task PadLock(int code)
        {
            var thing = Program.Configuration["tokens:padlock"];
            if(thing == code.ToString())
            {
                var webu = new WebUser()
                {
                    Username = "Teacher",
                    Discriminator = 1,
                    Id = 666
                };
                var bUser = Context.BotDB.GetUserFromDiscord(webu, true).Result.Value;
                var session = Context.GenerateNewSession(bUser, true);
                Context.HTTP.Response.AppendCookie(new Cookie(BotDbAuthSession.CookieName, session.Token, "/")
                {
                    Expires = DateTime.Now.AddDays(3)
                });
                await RespondRaw(session.Token, HttpStatusCode.OK);
            } else
            {
                await RespondRaw("Unknown code.", HttpStatusCode.Unauthorized);
            }
        }
    }
}
