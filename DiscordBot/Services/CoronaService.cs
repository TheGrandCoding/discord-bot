using Discord;
using Discord.WebSocket;
using DiscordBot.Classes.CoronAPI;
using DiscordBot.Services.BuiltIn;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class CoronaService : SavedService
    {
        public const string URL = @"https://corona-api.com/countries/";
        public const int IsolationPeriod = 10 * 24;

        public Dictionary<string, List<DateTime>> AverageUpdateTimes = new Dictionary<string, List<DateTime>>();
        public Dictionary<ulong, DateTime> Isolation = new Dictionary<ulong, DateTime>();
        public List<SendingEntry> Entries = new List<SendingEntry>();
        public DateTime lastDone = DateTime.Now;

        class save
        {
            public Dictionary<string, List<DateTime>> update;
            public List<SendingEntry> entries;
            public Dictionary<ulong, DateTime> isolation;
        }

        public override string GenerateSave()
        {
            var s = new save()
            {
                update = AverageUpdateTimes,
                entries = Entries,
                isolation = Isolation
            };
            return Program.Serialise(s);
        }

        public override void OnReady()
        {
            var s = ReadSave("{}");
            if(s[0] == '[')
            {
                Entries = Program.Deserialise<List<SendingEntry>>(s);
                AverageUpdateTimes = new Dictionary<string, List<DateTime>>();
            } else
            {
                var sv = Program.Deserialise<save>(s);
                AverageUpdateTimes = sv.update;
                Entries = sv.entries;
                Isolation = sv.isolation ?? new Dictionary<ulong, DateTime>();
            }
            var t = new Thread(threadWork);
            t.Start();
        }

        public override void OnDailyTick()
        {
            checkIsolationNicknames().Wait();
        }
        public override void OnLoaded()
        {
            OnDailyTick();
        }

        void handleTimeout()
        {
            var wanted = lastDone.AddHours(0.5);
            var diff = wanted - DateTime.Now;
            var ms = (int)Math.Ceiling(diff.TotalMilliseconds);
            if (ms <= 0)
                ms = (1000 * 60 * 15);
            Task.Delay(ms, Program.GetToken()).Wait();
        }

        string thousand(int input)
        {
            var builder = new StringBuilder();
            return input.ToString("#,##0");
        }

        const string flagUrl = @"https://www.countryflags.io/{0}/flat/64.png";
        public EmbedBuilder getEmbed(CoronaData data)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = $"Coronavirus -- {data.Name}";
            builder.ThumbnailUrl = string.Format(flagUrl, data.Code.ToLower());
            int millionsOfPop = (int)Math.Round(data.Population / (double)Amounts.Denary.Million);
            int casesPerMil = (data.Latest.Confirmed ?? 1) / millionsOfPop;
            builder.Description = $"Population: {thousand(data.Population)}\r\n" +
                $"Cases/mil: {thousand(casesPerMil)}"; 
            builder.Timestamp = data.UpdatedAt;
            int conf = data.Latest.Confirmed ?? 0;
            var confPerc = Math.Round(((double)conf / data.Population) * 100, 2);
            builder.AddField("Confirmed", $"{thousand(conf)} ({confPerc}%)", true);
            int recov = data.Latest.Recovered ?? 0;
            var recovPerc = Math.Round(((double)recov / (conf == 0 ? 1 : conf)) * 100, 1);
            builder.AddField("Recovered", $"{thousand(recov)} ({recovPerc}%)", true);
            int crit = data.Latest.Critical ?? 0;
            var critPerc = Math.Round(((double)crit / (conf == 0 ? 1 : conf)) * 100, 1);
            builder.AddField("Critical", $"{thousand(crit)} ({critPerc}%)", true);
            int dead = data.Latest.Deaths ?? 0;
            var deadPerc = Math.Round(((double)dead / (conf == 0 ? 1 : conf)) * 100, 1);
            builder.AddField("Deaths", $"{thousand(dead)} ({deadPerc}%)", true);
            builder.AddField("New Today", $"Confirmed: {thousand(data.Today.Confirmed ?? 0)}\n" +
                $"Deaths: {thousand(data.Today.Deaths ?? 0)}", true);
            // Rolling Average, last few days
            var interested = data.Timeline.Take(5).ToList();
            int newConfirmed = 0;
            int newDeaths = 0;
            int newRecovered = 0;
            foreach(var thing in interested)
            {
                newConfirmed += thing.NewConfirmed ?? 0;
                newDeaths += thing.NewDeaths ?? 0;
                newRecovered += thing.NewRecovered ?? 0;
            }
            var avgConfirmed = (int)Math.Round(newConfirmed / (double)interested.Count);
            var avgDeaths = (int)Math.Round(newDeaths / (double)interested.Count);
            var avgRecovered = (int)Math.Round(newRecovered / (double)interested.Count);
            builder.AddField($"Average {interested.Count} days",
                $"Confirmed: {thousand(avgConfirmed)}\r\n" +
                $"Deaths: {thousand(avgDeaths)}\r\n" +
                $"Recovered: {thousand(avgRecovered)}");

            var skipAfter = data.Timeline.Skip(5).Take(5).ToList();
            var priorConfirmed = skipAfter.Sum(x => x.NewConfirmed ?? 0);
            var avgPriorDaily = (int)Math.Round(priorConfirmed / (double)skipAfter.Count);

            var difference = avgConfirmed - avgPriorDaily;
            var percentage = difference / (double)avgPriorDaily;
            builder.AddField("Percentage", $"{(percentage > 0 ? "+" : "")}" +
                $"{Math.Round(percentage * 100, 1)}% from average 5 days ago");
            if(percentage > 0)
            {
                builder.Color = Color.Red;
            } else if (percentage <= 0)
            {
                builder.Color = casesPerMil <= 250 ? Color.Green : Color.Orange;
            }
            return builder;
        }

        string getPrefix(int hoursRemain)
        {
            if (hoursRemain <= 0)
                return null;
            var perc = hoursRemain / (double)IsolationPeriod;
            if (perc > 0.8)
                return Emotes.MICROBE.Name.Repeat(5);
            if (perc > 0.6)
                return Emotes.MICROBE.Name.Repeat(4);
            if (perc > 0.4)
                return Emotes.MICROBE.Name.Repeat(3);
            if (perc > 0.2)
                return Emotes.MICROBE.Name.Repeat(2);
            return Emotes.MICROBE.Name.Repeat(1);
        }

        string getTrueNickname(string name)
        {
            return name.Replace(Emotes.MICROBE.Name, "");
        }

        async Task checkIsolationNicknames()
        {
            var ls = new List<ulong>();
            foreach(var keypair in Isolation)
            {
                var end = keypair.Value.ToLastSecond();
                var bUser = Program.GetUserOrDefault(keypair.Key);
                var notify = bUser?.Options.WhenToNotifyIsolation ?? Classes.IsolationNotify.Never;
                var diffRemain = end - DateTime.Now;
                int hoursRemain = (int)Math.Round(diffRemain.TotalHours);
                Program.LogMsg($"{bUser.Name} has {hoursRemain} hours left of their isolation", LogSeverity.Info, "Corona");
                var prefix = getPrefix(hoursRemain);
                var anyValid = bUser.FirstValidUser;
                if(prefix == null)
                {
                    if (notify.HasFlag(Classes.IsolationNotify.End))
                        await anyValid?.SendMessageAsync(embed: new EmbedBuilder()
                            .WithTitle($"COVID Isolation Ended")
                            .WithDescription($"Your isolation period has ended.")
                            .Build());
                    ls.Add(keypair.Key);
                }
                else
                {
                    if (notify.HasFlag(Classes.IsolationNotify.Daily))
                        await anyValid?.SendMessageAsync(embed: new EmbedBuilder()
                            .WithTitle($"COVID Isolation Update")
                            .WithDescription($"You have {hoursRemain} hours of your isolation period remaining.")
                            .AddField("End Date", end.ToString("yyyy/MM/dd hh:mm:ss"))
                            .Build());
                }
                foreach(var guild in Program.Client.Guilds)
                {
                    var usr = guild.GetUser(keypair.Key);
                    if (usr == null)
                        continue;
                    var thing = getTrueNickname(usr.Nickname ?? usr.Username);
                    if (prefix == null)
                    {
                        if(usr.Hierarchy < guild.CurrentUser.Hierarchy)
                            await usr.ModifyAsync(x => x.Nickname = thing);
                    }
                    else
                    {
                        if (usr.Hierarchy < guild.CurrentUser.Hierarchy)
                            await usr.ModifyAsync(x => x.Nickname = prefix + thing);
                    }
                }
            }
            foreach (var x in ls) Isolation.Remove(x);
            if (ls.Count > 0)
                OnSave();
        }

        void threadWork()
        {
            try
            {
                withinTryWork();
            } catch (Exception ex)
            {
                Program.LogMsg("CoronaService", ex);
                HasFailed = true;
            }
            Console.WriteLine("Corona thread has ended!");
        }

        double getProbability(int c)
        {
            switch(c)
            {
                case 0:
                case 1:
                    return 1d;
                case 2:
                    return 0.95d;
                case 3:
                    return 0.75d;
                case 4:
                case 5:
                    return 0.5;
                case 6:
                case 7:
                case 8:
                    return 0.3;
                case 9:
                    return 0.2;
                case 10:
                    return 0.1;
                default:
                    return 0.01;
            }
        }

        bool shouldDoEntryNow(string code)
        {
            List<DateTime> times;
            if (!AverageUpdateTimes.TryGetValue(code, out times))
                times = new List<DateTime>();
            if (times.Any(x => x.DayOfYear == DateTime.Now.DayOfYear))
                return false;
            var odds = getProbability(times.Count);
            if (Program.RND.NextDouble() < odds)
                return true;
            var now = DateTime.Now;
            var time = new DateTime(now.Year, now.Month, now.Day).AddSeconds(times.Select(x => x.TimeOfDay.TotalSeconds).Average());
            return now > time;
        }

        void withinTryWork()
        {
            var client = Program.Services.GetRequiredService<HttpClient>();
            while(this.IsEnabled && Program.GetToken().IsCancellationRequested == false)
            {
                handleTimeout();
                Dictionary<string, CoronaData> dataDict = new Dictionary<string, CoronaData>();
                var codes = Entries.Select(x => x.Code).Distinct();
                bool anyDone = false;
                foreach(var entry in Entries)
                {
                    if(!dataDict.ContainsKey(entry.Code) && shouldDoEntryNow(entry.Code))
                    {
                        anyDone = true;
                        var stamp = new Stopwatch();
                        stamp.Start();
                        var req = new HttpRequestMessage(HttpMethod.Get, $"{URL}{entry.Code}");
                        var response = client.SendAsync(req).Result;
                        stamp.Stop();
                        string text = response.Content.ReadAsStringAsync().Result;
                        if (!response.IsSuccessStatusCode)
                        {
                            var jobj = JObject.Parse(text);
                            var message = jobj["message"];
                            Program.LogMsg($"Failed to operate {entry.Code} {entry.Channel.Guild.Name}: {message}");
                            entry.Channel.SendMessageAsync($"Failed to fetch {entry.Code} coronavirus stats: `{message}`");
                            continue;
                        }
                        var data = Program.Deserialise<CoronaResponse>(text);
                        if(DateTime.Now > data.Data.UpdatedAt)
                        {
                            List<DateTime> ls;
                            if (!AverageUpdateTimes.TryGetValue(entry.Code, out ls))
                            {
                                ls = new List<DateTime>();
                                AverageUpdateTimes[entry.Code] = ls;
                            }
                            if(ls.Count == 0 || ls.Last() < data.Data.UpdatedAt)
                            {
                                ls.Add(data.Data.UpdatedAt);
                                Program.LogMsg($"Update {entry.Code} at {data.Data.UpdatedAt.TimeOfDay}", LogSeverity.Info, "CoronaAPI");
                            }
                        }
                        dataDict[entry.Code] = data.Data;
                        if (stamp.ElapsedMilliseconds < 1500)
                        {
                            Program.LogMsg("Invoking pre-emptive rate-limit", LogSeverity.Info, "CoronaAPI");
                            Thread.Sleep(1500);
                        }
                    }
                }
                foreach(var entry in Entries)
                {
                    if (!(dataDict.TryGetValue(entry.Code, out var data)))
                        continue;
                    var builder = getEmbed(data);
                    if(entry.Message == null)
                    {
                        entry.Message = entry.Channel.SendMessageAsync(embed: builder.Build()).Result;
                    } else
                    {
                        entry.Message.ModifyAsync(x =>
                        {
                            x.Embed = builder.Build();
                        });
                    }
                }
                if(anyDone)
                {
                    lastDone = DateTime.Now;
                    this.OnSave();
                }
            }
        }
    }

    public class SendingEntry
    {
        public IUserMessage Message { get; set; }
        public ITextChannel Channel { get; set; }
        public string Code { get; set; }
    }
}
