using Discord.Commands;
using Markdig.Extensions.DefinitionLists;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Name("Time Module")]
    [Group("time")]
    public class TimeModule : BotBase
    {
        string encodeName(string name)
        {
            return name
                .Replace(" Standard Time", "")
                .Replace("Time Zone", "TZ");
        }
        TimeZoneInfo getTimeZone(TimeSpan distanceFromUtc)
        {
            foreach(var tz in TimeZoneInfo.GetSystemTimeZones())
            {
                var offset = tz.BaseUtcOffset;
                if (offset.Hours == distanceFromUtc.Hours && offset.Minutes == distanceFromUtc.Minutes)
                    return tz;
            }
            return TimeZoneInfo.CreateCustomTimeZone($"Unknown", distanceFromUtc, 
                $"(UTC {distanceFromUtc.Hours:00}:{distanceFromUtc.Minutes:00}) Unknown", "Unknown");
        }
        TimeZoneInfo getTimeZone(string text)
        {
            foreach(var tz in TimeZoneInfo.GetSystemTimeZones())
            {
                if (encodeName(tz.Id) == text)
                    return tz;
                if (tz.DisplayName.Contains(text))
                    return tz;
            }
            throw new InvalidTimeZoneException($"No time zone recognised by that name");
        }

        [Command("zones"), Alias("zones")]
        [Summary("Lists all timezones known")]
        public async Task Zones()
        {
            var sb = new StringBuilder();
            sb.Append($"Timezones:");
            foreach(var tz in TimeZoneInfo.GetSystemTimeZones())
            {
                if (tz.Id.EndsWith(")"))
                    continue;
                if (tz.Id.StartsWith("UTC"))
                    continue;
                if (tz.Id.EndsWith("Standard Time") == false)
                    continue;
                var thing = $"\r\n`{encodeName(tz.Id)}` {tz.BaseUtcOffset.Hours:00}:{tz.BaseUtcOffset.Minutes:00}";
                if (sb.Length + thing.Length < 2000)
                    sb.Append(thing);
                else
                    break;
            }
            await ReplyAsync(sb.ToString());
        }
        [Command("now")]
        [Summary("Views current time, in an optional timezone or difference")]
        public async Task SeeCurrentTime(string zone = null)
        {
            TimeZoneInfo tz;
            int diff = 0;
            if(zone == null || int.TryParse(zone, out diff))
            {
                tz = TimeZoneInfo.Local;
                tz = getTimeZone(tz.BaseUtcOffset.Add(TimeSpan.FromHours(diff)));
            } else
            {
                tz = getTimeZone(zone);
            }
            var thus = TimeZoneInfo.ConvertTime(DateTime.Now, tz);
            await ReplyAsync($"{thus.Hour:00}:{thus.Minute:00}:{thus.Second:00} {tz.DisplayName}");
        }
    }
}
