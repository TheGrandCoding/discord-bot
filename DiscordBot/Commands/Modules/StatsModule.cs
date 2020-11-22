using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Name("Message Stats")]
    [Group("stats")]
    public class StatsModule : BotModule
    {
        public static Dictionary<ulong, CancellationTokenSource> Tokens = new Dictionary<ulong, CancellationTokenSource>();
        [Command("run")]
        [Summary("Does some statistics checks in this channel")]
        public async Task Run(int maximumMessages = 1000, bool includeBots = false)
        {
            if(Tokens.TryGetValue(Context.Channel.Id, out var src))
            {
                await ReplyAsync("This channel is already being looked at.");
                return;
            }
            if(Tokens.Count > 0)
            {
                var existing = Tokens.Keys.First();
                await ReplyAsync($"A statistics check is already in progress for <#{existing}>");
                return;
            }
            var msg = await ReplyAsync(embed: new EmbedBuilder()
                .WithDescription("Starting stats checks...")
                .Build());
            src = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            var stats = new Statistics()
            {
                Channel = Context.Channel as ITextChannel,
                Status = msg,
                Maximum = maximumMessages,
                Remaining = maximumMessages,
                Token = src.Token,
                IncludeBots = includeBots
            };
            Tokens[Context.Channel.Id] = src;
            stats.Start();
        }

        [Command("stop")]
        [Summary("Stops current stats checks")]
        public async Task Cancel()
        {
            if (Tokens.TryGetValue(Context.Channel.Id, out var src))
            {
                await ReplyAsync("Stopping...");
                src.Cancel();
            } else
            {
                await ReplyAsync("This channel has no statistics ongoing");
            }
        }

        [Command("fstop")]
        [Summary("Force stops all stats checks")]
        [RequireOwner]
        public async Task FStop()
        {
            foreach (var x in Tokens.Values)
                x.Cancel();
            Tokens = new Dictionary<ulong, CancellationTokenSource>();
        }
    }

    public class Statistics
    {
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
        public CancellationToken Token { get; set; }
        public Dictionary<ulong, ValueStats> Stats { get; set; } = new Dictionary<ulong, ValueStats>();
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
        }

        public void wrappedThread()
        {
            try
            {
                threadLoop();
            } catch(Exception ex)
            {
                Program.LogMsg("Stats", ex);
                try
                {
                    Status.ModifyAsync(x => x.Content = $"Erorr ocurred: {ex}");
                } catch { }
            } 
            finally
            {
                StatsModule.Tokens.Remove(Channel.Id);
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
                    Program.LogMsg("Stats", ex);
                    try
                    {
                        Status.ModifyAsync(x => x.Content = $"Error occured whilst handling this: {ex}");
                    } catch { }
                }
            } while (Remaining > 0 && !Token.IsCancellationRequested && AllStats.TotalSent < Maximum);
            Thread.Sleep(10 * 1000);
            SendUpdate();
        }

        public void Update()
        {
            if(LastSentUpdate == null || (DateTime.Now - LastSentUpdate.Value).TotalSeconds > 10)
            {
                LastSentUpdate = DateTime.Now;
                SendUpdate();
            }
        }
        public void SendUpdate()
        {
            var _ = Task.Run(async () =>
            {
                await Status.ModifyAsync(x => x.Embed = ToEmbed().Build());
                if (Token.IsCancellationRequested || Remaining <= 0)
                    await Channel.SendMessageAsync($"Stats checks have finished. See {Status.GetJumpUrl()}");
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
            builder.Description = $"Global " + AllStats.ToString();
            var ordered = Stats.OrderByDescending(x => x.Value.TotalSent).Where(x => x.Key != 0).Select(x => x.Key).ToList();
            if(ordered.Count > max)
            {
                var os = new ValueStats();
                Stats[0] = os;
                DisplayNames[0] = $"Other ({max - ordered.Count})";
                foreach(var other in ordered.Skip(max))
                {
                    // new = old + (val - old / n)
                    var ex = Stats[other];
                    for(int i = 0; i < ex.TotalSent; i++)
                    {
                        os.TotalSent++;
                        os.AddAverage(ex.AverageLength, ref os.AverageLength);
                        os.AddAverage(ex.AverageSecondsIntoDay, ref os.AverageSecondsIntoDay);
                    }
                }
                ordered = ordered.Take(max).ToList();
                ordered.Add(0);
            }
            foreach(var id in ordered)
            {
                var stats = Stats[id];
                var percSent = (stats.TotalSent / (double)AllStats.TotalSent) * 100;
                var val = stats.ToString();
                val += $"\r\nPerc: {percSent:00.0}%";
                var key = DisplayNames.GetValueOrDefault(id, id.ToString());
                builder.AddField(key, val, true);
            }
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

    }

}
