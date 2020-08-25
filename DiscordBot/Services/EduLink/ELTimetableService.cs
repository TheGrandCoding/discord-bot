using DiscordBot.Classes;
using DiscordBot.Classes.Attributes;
using EduLinkDLL;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services.EduLink
{
    [RequireService(typeof(EduLinkService))]
    public class ELTimetableService : Service
    {
        public EduLinkService EduLink { get; set; }

        public override void OnLoaded()
        {
            EduLink = Program.Services.GetRequiredService<EduLinkService>();
            foreach(var uId in EduLink.Clients.Keys)
            {
                var bUser = Program.GetUserOrDefault(uId);
                if(bUser.Classes.Count == 0)
                {
                    var client = EduLink.Clients[uId];
                    SetClasses(bUser, client).Wait();
                }
            }
        }

        public async Task<string[]> SetClasses(BotUser bUser, EduLinkClient client)
        {
            bUser.Classes = new Dictionary<string, string>();
            var timetable = await client.TimetableAsync();
            foreach (var week in timetable)
            {
                foreach (var day in week.Days)
                {
                    foreach (var lesson in day.Lessons)
                    {
                        bUser.Classes[lesson.TeachingGroup.Name] = lesson.TeachingGroup.Subject ?? "unknown";
                    }
                }
            }
            return bUser.Classes.Keys.ToArray();
        }
    }
}
