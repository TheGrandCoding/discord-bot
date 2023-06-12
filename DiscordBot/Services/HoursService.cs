using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Classes.DbContexts;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class HoursService : Service
    {
        public HoursDbContext DB()
        {
            return Program.GlobalServices.GetRequiredService<HoursDbContext>();
        }

        public override void OnReady(IServiceProvider services)
        {
            Program.Client.PresenceUpdated += Client_PresenceUpdated;
        }

        private ConcurrentDictionary<ulong, DateTime> clientUpdateCache = new();
        private async Task Client_PresenceUpdated(Discord.WebSocket.SocketUser arg1, Discord.WebSocket.SocketPresence arg2, Discord.WebSocket.SocketPresence arg3)
        {
            if(arg2.Status != arg3.Status)
            {
                var now = DateTime.UtcNow;
#if !DEBUG
                if (now.DayOfWeek == DayOfWeek.Sunday || now.DayOfWeek == DayOfWeek.Saturday) return;
#endif
                bool process = true;
                if(clientUpdateCache.TryGetValue(arg1.Id, out var time))
                {
                    process = (now - time).TotalSeconds > 5;
                }
                clientUpdateCache[arg1.Id] = now;
                if(process)
                {
                    Info($"{arg1.Id} {arg1.Username} status from {arg2.Status} -> {arg3.Status}", "PresenceUpdated");
                    if(arg2.Status == Discord.UserStatus.Offline)
                    {
                        await processUser(arg1);
                    }
                }
            }
        }

        TimeSpan getSpan(string time)
        {
            var split = time.Split(':');
            return new TimeSpan(int.Parse(split[0]), int.Parse(split[1]), 0);
        }

        async Task processUser(SocketUser user)
        {
            var now = DateTime.Now;
            var nowDate = now.Date;

            HoursSettings currentSetting = null;

            using (var db = DB())
            {
                IReadOnlyCollection<HoursSettings> settings;
                settings = db.GetSettings(user.Id);

                currentSetting = settings.FirstOrDefault(x => now >= x.StartDate && now < x.EndDate);
                if (currentSetting == null) return;

                var todayEntry = db.GetEntries(user.Id, currentSetting.Id, nowDate.AddSeconds(-10), nowDate.ToLastSecond());
                if(todayEntry != null && todayEntry.Count > 0)
                {
                    Info($"{user.Id} already has a DB entry today.");
                    return;
                }
            }

            var startTime = now.Date.Add(getSpan(currentSetting.ExpectedStartTime));
            var endTime = now.Date.Add(getSpan(currentSetting.ExpectedEndTime));

            if(now < startTime)
            {
                Info($"{user.Id} went online, but this is before they are expected to start. Ignoring", "Process");
                return;
            }
            if(now < endTime)
            {
                Info($"{user.Id} went online, but this is before they are expected to finish. Ignoring", "Process");
                return;
            }
            // user has went online after they're expected to finish, just see how long..

            var breakHours = currentSetting.ExpectedBreak;
            var normalHours = (endTime - startTime).TotalHours - breakHours;

            double overtime;

            var diff = now - endTime;
            if(diff.TotalHours <= 1)
            {
                Info($"{user.Id} went online within an hour of expected end, assuming a regular day of {normalHours} hours", "Process");
                overtime = 0;
            } else
            {
                overtime = Math.Ceiling(diff.TotalHours - 1);
                Info($"{user.Id} went online over expected end, assuming {normalHours} + {overtime} hours", "Process");
            }

            var embed = new EmbedBuilder();
            embed.Title = "Verify Hours";
            embed.Description = $"Please verify the followng hours for today";
            embed.AddField("Regular", $"{normalHours:0.0}", true);
            embed.AddField("Overtime", $"{overtime:0.0}", true);
            embed.AddField("Break", $"{breakHours:0.0}", true);
            embed.WithFooter(currentSetting.Id);

            var components = new ComponentBuilder();

            components.WithButton("Confirm", $"hours:confirm", ButtonStyle.Success);
            if(overtime > 0)
            {
                components.WithButton("No Overtime", "hours:noover", ButtonStyle.Primary);
            } else
            {
                components.WithButton("1hr Overtime", "hours:1hr", ButtonStyle.Secondary);
            }
            components.WithButton("Edit", "hours:edit", ButtonStyle.Danger);


            var dm = await user.CreateDMChannelAsync();

            await dm.SendMessageAsync(embeds: new[] { embed.Build() }, components: components.Build());
        }

    }

    public class HoursSettings
    {
        public string Id { get; set; }
        public long UserId { get; set; }

        [NotMapped]
        public ulong User {
            get
            {
                unchecked
                {
                    return (ulong)UserId;
                }
            }

            set
            {
                unchecked
                {
                    UserId = (long)value;
                }
            }
        }
    
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string ExpectedStartTime { get; set; }
        public string ExpectedEndTime { get; set; }

        public double ExpectedBreak { get; set; }

        public double NormalRate { get; set; }
        public double OvertimeRate { get; set; }

    }

    public class HoursEntry
    {
        public string SettingId { get; set; }
        public long UserId { get; set; }

        [NotMapped]
        public ulong User
        {
            get
            {
                unchecked
                {
                    return (ulong)UserId;
                }
            }

            set
            {
                unchecked
                {
                    UserId = (long)value;
                }
            }
        }

        public DateTime Date { get; set; }

        public double NormalHours { get; set; }
        public double OvertimeHours { get; set; }
        public double BreakHours { get; set; }

    }

}
