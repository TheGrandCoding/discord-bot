using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.MLAPI;
using DiscordBot.Permissions;
using DiscordBot.Services;
using DiscordBot.Utils;
using DiscordBot.Websockets;
using Google.Apis.Util;
using Markdig.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Name("Message Stats")]
    [Group("stats")]
    public class StatsModule : BotBase
    {
        //public static Dictionary<ulong, CancellationTokenSource> Tokens = new Dictionary<ulong, CancellationTokenSource>();
        public static Dictionary<ulong, Statistics> RunningStats { get; set; } = new Dictionary<ulong, Statistics>();
        public static Dictionary<int, Statistics> AllStats { get; set; } = new Dictionary<int, Statistics>();


        [Command("run")]
        [Summary("Does some statistics checks in the given channel")]
        public async Task Run(ITextChannel channel, int maximumMessages = 1000, bool includeBots = false)
        {
            if (RunningStats.TryGetValue(channel.Id, out var stats))
            {
                await ReplyAsync("This channel is already being looked at.");
                return;
            }
            if (RunningStats.Count > 0)
            {
                var existing = RunningStats.Keys.First();
                await ReplyAsync($"A statistics check is already in progress for <#{existing}>, and only one can occur at a time");
                return;
            }
            var msg = await ReplyAsync(embed: new EmbedBuilder()
                .WithDescription("Starting stats checks...")
                .Build());
            var src = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            stats = new Statistics()
            {
                Channel = channel as ITextChannel,
                Status = msg,
                Maximum = maximumMessages,
                Remaining = maximumMessages,
                TokenSource = src,
                Token = src.Token,
                IncludeBots = includeBots
            };
            await msg.ModifyAsync(x => x.Embed = stats.ToEmbed().Build());
            RunningStats[channel.Id] = stats;
            AllStats[stats.Id] = stats;
            stats.Start();
        }



        [Command("run")]
        [Summary("Does some statistics checks in this channel")]
        public async Task Run(int maximumMessages = 1000, bool includeBots = false)
            => await Run(Context.Channel as ITextChannel, maximumMessages, includeBots);


        [Command("stop")]
        [Summary("Stops current stats checks")]
        public async Task Cancel()
        {
            if (RunningStats.TryGetValue(Context.Channel.Id, out var src))
            {
                await ReplyAsync("Stopping...");
                src.TokenSource.Cancel();
            } else
            {
                await ReplyAsync("This channel has no statistics ongoing");
            }
        }

        [Command("fstop")]
        [Summary("Force stops all stats checks")]
        [Discord.Commands.RequireOwner]
        public async Task FStop()
        {
            foreach (var x in RunningStats.Values)
                x.TokenSource.Cancel();
            RunningStats = new Dictionary<ulong, Statistics>();
        }
    }

    public class Statistics
    {
        static int _id = 0;
        public Statistics()
        {
            Id = System.Threading.Interlocked.Increment(ref _id);
        }

        public int Id { get; set; }
        public void Start()
        {
            var th = new Thread(wrappedThread);
            th.Start();
        }
        public ITextChannel Channel { get; set; }
        public IUserMessage Status { get; set; }
        public int Maximum = int.MaxValue;
        public int Remaining;
        public bool IncludeBots { get; set; }
        public CancellationTokenSource TokenSource { get; set; }
        public CancellationToken Token { get; set; }
        public Dictionary<ulong, ValueStats> Stats { get; set; } = new Dictionary<ulong, ValueStats>();
        public Dictionary<string, int> WordCount { get; set; } = new Dictionary<string, int>();
        public Dictionary<ulong, string> DisplayNames { get; set; } = new Dictionary<ulong, string>();
        public ValueStats AllStats = new ValueStats();
        public DateTime StartedAt { get; set; } = DateTime.Now;
        public DateTime? LastSentUpdate = null;

        public void Add(IUserMessage message)
        {
            if(!Stats.TryGetValue(message.Author.Id, out var stats))
            {
                stats = new ValueStats();
                Stats[message.Author.Id] = stats;
            }
            stats.TotalSent++;
            stats.AddAverage(message.Content.Length, ref stats.AverageLength);
            var midnight = new DateTime(message.Timestamp.Year, message.Timestamp.Month, message.Timestamp.Day);
            var diff = message.Timestamp - midnight;
            stats.AddAverage(diff.TotalSeconds, ref stats.AverageSecondsIntoDay);

            AllStats.TotalSent++;
            AllStats.AddAverage(message.Content.Length, ref AllStats.AverageLength);
            AllStats.AddAverage(diff.TotalSeconds, ref AllStats.AverageSecondsIntoDay);

            if (message.Content.StartsWith("$") || message.Content.StartsWith("^"))
                return;
            foreach(var word in message.Content.Split(" "))
            {
                if (word.Length >= 3 && word.All(x => x.IsAlphaNumeric()))
                    WordCount.Increment(word.ToLower());
            }
        }

        public void wrappedThread()
        {
            try
            {
                threadLoop();
            } catch(Exception ex)
            {
                Program.LogError(ex, "Stats");
                try
                {
                    Status.ModifyAsync(x => x.Content = $"Erorr ocurred: {ex}");
                } catch { }
            } 
            finally
            {
                StatsModule.RunningStats.Remove(Channel.Id);
            }
        }

        public void threadLoop()
        {
            ulong? lastMessage = null;
            do
            {
                try
                {
                    IEnumerable<IMessage> messages;
                    int lim = Math.Clamp(Remaining, 1, 100);
                    if (lastMessage == null)
                        messages = Channel.GetMessagesAsync(lim).FlattenAsync().Result;
                    else
                        messages = Channel.GetMessagesAsync(lastMessage.Value, Direction.Before, lim).FlattenAsync().Result;
                    lastMessage = messages.LastOrDefault()?.Id;
                    var any = false;
                    foreach(var msg in messages)
                    {
                        if (!(msg is IUserMessage usm))
                            continue;
                        if (msg.Author.IsBot && !IncludeBots)
                            continue;
                        if (msg.Author.IsWebhook)
                            continue;
                        any = true;
                        if (!DisplayNames.ContainsKey(msg.Author.Id))
                            DisplayNames[msg.Author.Id] = $"{msg.Author.Username}#{msg.Author.Discriminator}";
                        Add(usm);
                        Remaining--;
                        Update();
                        if (Remaining <= 0 || Token.IsCancellationRequested)
                            break;
                    }
                    if(!any)
                    {
                        Remaining = -1;
                    }
                    if (Remaining <= 0 || Token.IsCancellationRequested)
                        break;
                } catch(Exception ex)
                {
                    Program.LogError(ex, "Stats");
                    try
                    {
                        Status.ModifyAsync(x => x.Content = $"Error occured whilst handling this: {ex}");
                    } catch { }
                }
            } while (Remaining > 0 && !Token.IsCancellationRequested && AllStats.TotalSent < Maximum);
            Task.Delay(10_000, Program.GetToken()).Wait();
            LastSentUpdate = null;
            Update();
        }

        public void Update()
        {
            bool sentAny = false;
            if(LastSentUpdate == null || (DateTime.Now - LastSentUpdate.Value).TotalSeconds > 3)
            {
                sentAny = true;
                SendWebsockets();
            }
            if (LastSentUpdate == null || (DateTime.Now - LastSentUpdate.Value).TotalSeconds > 15)
            {
                sentAny = true;
                SendEmbed();
            }
            if (sentAny)
                LastSentUpdate = DateTime.Now;
        }

        Dictionary<string, int> GetWords()
        {
            var words = WordCount.OrderByDescending(x => x.Value);
            var dict = new Dictionary<string, int>();
            foreach(var keypair in words)
            {
                dict[keypair.Key] = keypair.Value;
                if (dict.Count >= 100)
                    break;
            }
            return dict;
        }

        public void SendEmbed()
        {

            var _ = Task.Run(async () =>
            {
                await Status.ModifyAsync(x => x.Embed = ToEmbed().Build());
                if (Token.IsCancellationRequested || Remaining <= 0)
                {
                    await Status.Channel.SendMessageAsync($"Stats checks have finished. See {Status.GetJumpUrl()}");
                }
            });
        }

        public JObject GetJson()
        {
            var ordered = Stats.OrderByDescending(x => x.Value.TotalSent).Where(x => x.Key != 0).Select(x => x.Key).ToList();
            if (ordered.Count > max)
            {
                var os = new ValueStats();
                Stats[0] = os;
                DisplayNames[0] = $"Other ({max - ordered.Count})";
                foreach (var other in ordered.Skip(max))
                {
                    // new = old + (val - old / n)
                    var ex = Stats[other];
                    for (int i = 0; i < ex.TotalSent; i++)
                    {
                        os.TotalSent++;
                        os.AddAverage(ex.AverageLength, ref os.AverageLength);
                        os.AddAverage(ex.AverageSecondsIntoDay, ref os.AverageSecondsIntoDay);
                    }
                }
                ordered = ordered.Take(max).ToList();
                ordered.Add(0);
            }
            var jobj = new JObject();
            jobj["total"] = AllStats.ToJson();
            foreach (var id in ordered)
            {
                var stats = Stats[id];
                var key = DisplayNames.GetValueOrDefault(id, id.ToString());
                var user = Program.Client.GetUser(id);
                var obj = new JObject();
                var usrObj = new JObject();
                usrObj["username"] = user?.Username ?? key;
                usrObj["avatar"] = user?.GetAnyAvatarUrl() ?? "n/a";
                obj["user"] = usrObj;
                obj["stats"] = stats.ToJson();
                jobj[id.ToString()] = obj;
            }
            jobj["words"] = JObject.FromObject(GetWords());
            return jobj;
        }

        public void SendWebsockets()
        {
            var _ = Task.Run(async () =>
            {
                var jobj = GetJson().ToString();
                if (WSService.Server.WebSocketServices.TryGetServiceHost($"/statistics", out var host))
                {
                    foreach(var session in host.Sessions.Sessions)
                    {
                        if (session is StatisticsWS ws && ws.ListeningTo == Id)
                            host.Sessions.SendTo(jobj, ws.ID);
                    }
                }
            });
        }

#if DEBUG
        const int max = 2;
#else
        const int max = 24;
#endif
        public EmbedBuilder ToEmbed()
        {
            var builder = new EmbedBuilder();
            builder.Title = $"Last {Maximum} Messages";
            if (Status.Channel.Id != Channel.Id)
                builder.Title += " in #" + Channel.Name;
            builder.Url = Handler.LocalAPIUrl + $"/statistics?id={Id}";
            builder.Description = $"Overall statistics:\r\n" + AllStats.ToString() + 
                $"\r\nUnique words: {WordCount.Count}" +
                $"\r\nTotal words sent: {WordCount.Values.Sum()}" +
                $"\r\n[Click here]({builder.Url}) to view stats, with a cool pie chart";
            var duration = DateTime.Now - StartedAt;
            builder.WithFooter($"Elapsed: {duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}");
            return builder;
        }
    }

    public class ValueStats
    {
        public int TotalSent;
        public double AverageLength;
        public double AverageSecondsIntoDay;

        public void AddAverage(double newValue, ref double average)
        {
            average = average + ((newValue - average) / TotalSent);
        }

        public override string ToString()
        {
            var now = new DateTime(1, 1, 1).AddSeconds(AverageSecondsIntoDay);
            return $"Total: {TotalSent}\r\n" +
                $"Avg. Length: {Math.Round(AverageLength, 2)}\r\n" +
                $"Avg. Time: {now:HH:mm:ss}";
        }

        public JObject ToJson()
        {
            var jobj = new JObject();
            jobj["count"] = TotalSent;
            jobj["avgLength"] = AverageLength;
            jobj["avgTime"] = AverageSecondsIntoDay;
            return jobj;
        }
    }

}
