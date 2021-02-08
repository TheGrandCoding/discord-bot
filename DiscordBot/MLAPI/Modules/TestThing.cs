using DiscordBot.Classes;
using DiscordBot.Classes.HTMLHelpers.Objects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    [RequireAuthentication(false)]
    public class TestThing : APIBase
    {
        public TestThing(APIContext c) : base(c, "_other") { }

        [Method("POST"), Path("/testthing")]
        public void Thing(string location, int people, string start, string end)
        {
            if(!DateTime.TryParseExact(start, "yyyy-MM-dd", CultureInfo.CurrentCulture, DateTimeStyles.None, out var startDate))
            {
                RespondRaw("Could not parse start date", 400);
                return;
            }
            if (!DateTime.TryParseExact(end, "yyyy-MM-dd", CultureInfo.CurrentCulture, DateTimeStyles.None, out var endDate))
            {
                RespondRaw("Could not parse end date", 400);
                return;
            }
            var diff = endDate - startDate;
            var hours = Math.Abs(diff.TotalHours);
            var price = Math.Round(hours * people, 2);
            ReplyFile("testthing.html", 200,
                new Replacements()
                .Add("location", location)
                .Add("duration", $"{diff.TotalDays:00} day(s)")
                .Add("price", $"£{price:000.00}"));
        }

        [Method("PUT"), Path("/testthing")]
        public void PadLock(int code)
        {
            var thing = Program.Configuration["tokens:padlock"];
            if(thing == code.ToString())
            {
                var bUser = Program.GetUserOrDefault(666);
                if(bUser == null)
                {
                    new BotUser(new WebUser()
                    {
                        Username = "Teacher",
                        Discriminator = 1,
                        Id = 666
                    });
                    Program.Users.Add(bUser);
                }
                var token = bUser.Tokens.FirstOrDefault(x => x.Name == AuthToken.HttpFullAccess);
                if(token == null)
                {
                    token = new AuthToken(AuthToken.HttpFullAccess, 24);
                    bUser.Tokens.Add(token);
                }
                Context.HTTP.Response.AppendCookie(new Cookie(AuthToken.SessionToken, token.Value, "/")
                {
                    Expires = DateTime.Now.AddDays(3)
                });
                RespondRaw(token.Value, HttpStatusCode.OK);
            } else
            {
                RespondRaw("Unknown code.", HttpStatusCode.Unauthorized);
            }
        }
    }
}
