using Discord;
using Discord.Commands;
using DiscordBot.Services.Timing;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules.Timing
{
    [Name("Countdown Module")]
    [Summary("Commands to count down to a specific date")]
    [Group("countdown")]
    public class CountdownModule : BotModule
    {
        public CountdownService Service { get; set; }

        bool canView(Countdown c)
        {
            var chnl = c.GetChannel();
            return chnl.GetUserAsync(Context.User.Id).Result != null;
        }

        string format(DateTime date)
        {
            var now = DateTime.Now;
            if(date.Year == now.Year && date.DayOfYear == now.DayOfYear)
            {
                var ts = date - now;
                return Program.FormatTimeSpan(ts, true);
            }
            return date.ToString("yyyy/MM/dd hh:mm:ss");
        }

        [Command("list")]
        [Summary("Lists all current countdowns")]
        public async Task List()
        {
            var embed = new EmbedBuilder();
            Service.Lock(() =>
            {
                foreach (var cnt in Service.Countdowns)
                {
                    if (!canView(cnt))
                        continue;
                    embed.AddField(format(cnt.End), $"{cnt.Text}");
                }
            });
            await ReplyAsync(embed: embed.Build());
        }

        [Command("on"), Alias("date")]
        [Summary("Counts down to the specified date, then sends the message")]
        public async Task CountTo(DateTime date, [Remainder]string message)
        {
            var cnt = new Countdown()
            {
                Started = DateTime.Now,
                End = date,
                User = Context.User,
                Text = message
            };
            if (Context.Channel is ITextChannel txt)
                cnt.Channel = txt;
            Service.Lock(() =>
            {
                Service.Countdowns.Add(cnt);
            });
            Success("Created countdown successfully.");
        }
        [Command("after"), Alias("duration", "time")]
        [Summary("Counts down after the specified duration has passed, then sends the message")]
        public async Task CountTo(TimeSpan time, [Remainder]string message)
        {
            var now = DateTime.Now;
            var cnt = new Countdown()
            {
                Started = now,
                End = now.Add(time),
                User = Context.User,
                Text = message
            };
            if (Context.Channel is ITextChannel txt)
                cnt.Channel = txt;
            Service.Lock(() =>
            {
                Service.Countdowns.Add(cnt);
            });
            Success("Created countdown successfully.");
        }
    }
}
