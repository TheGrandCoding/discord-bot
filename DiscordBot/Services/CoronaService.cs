using Discord;
using Discord.WebSocket;
using DiscordBot.Classes.CoronAPI;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;

namespace DiscordBot.Services
{
    public class CoronaService : SavedService
    {
        public const string URL = @"https://corona-api.com/countries/";
        public List<SendingEntry> Entries = new List<SendingEntry>();

        public override string GenerateSave()
        {
            return Program.Serialise(Entries);
        }

        public override void OnReady()
        {
            var s = ReadSave("[]");
            Entries = Program.Deserialise<List<SendingEntry>>(s);
            var t = new Thread(threadWork);
            t.Start();
        }

        void handleTimeout()
        {
            var wanted = new DateTime(DateTime.Now.Year,
                DateTime.Now.Month,
                DateTime.Now.Day,
                DateTime.Now.Hour,
                0,
                0);
            wanted = wanted.AddHours(3);
            var diff = wanted - DateTime.Now;
            Thread.Sleep((int)Math.Ceiling(diff.TotalMilliseconds));
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

        void threadWork()
        {
            var client = Program.Services.GetRequiredService<HttpClient>();
            while(this.IsEnabled)
            {
                handleTimeout();
                Dictionary<string, CoronaData> dataDict = new Dictionary<string, CoronaData>();
                var codes = Entries.Select(x => x.Code).Distinct();
                foreach(var entry in Entries)
                {
                    if(!dataDict.ContainsKey(entry.Code))
                    {
                        var req = new HttpRequestMessage(HttpMethod.Get, $"{URL}{entry.Code}");
                        var response = client.SendAsync(req).Result;
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
                        dataDict[entry.Code] = data.Data;
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
                this.OnSave();
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
