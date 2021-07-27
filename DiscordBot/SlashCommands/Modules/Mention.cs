using Discord;
using Discord.SlashCommands;
using Discord.WebSocket;
using Discord.Webhook;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiscordBot.Utils;
using System.Linq;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.SlashCommands.Modules
{
    [CommandGroup("mention", "Allows mentioning of a user en-masse")]
    public class Mention : BotSlashBase
    {
        public MentionService Service { get; set; }
        [SlashCommand("start", "Starts a mention of the user")]
        public async Task StartMention([Required] SocketGuildUser user, 
            [Choice("15 seconds", Time.S.Second * 15)]
            [Choice("30 seconds", Time.S.Second * 30)]
            [Choice("1 minute", Time.S.Minute)]
            [Choice("5 minutes", Time.S.Minute * 5)]
            [Choice("10 minutes", Time.S.Minute * 10)]
            int duration = Time.S.Second * 30)
        {
            await Interaction.AcknowledgeAsync(InteractionResponseFlags.Ephemeral);
            Service ??= Program.Services.GetRequiredService<MentionService>();
            Service.AddMention(Interaction.Channel as SocketTextChannel, Interaction.User as SocketGuildUser, user, duration);
            await Interaction.FollowupAsync($"Mention has begun!");
        }
    }

    public class MentionService : Service
    {
        public ConcurrentDictionary<ulong, MentionInfo> Mentions { get; set; } = new ConcurrentDictionary<ulong, MentionInfo>();

        public override void OnReady()
        {
            Program.Client.MessageReceived += async (SocketMessage arg) =>
            {
                if (arg.Author != null)
                    StopMention(arg.Author.Id);
            };
            Program.Client.ReactionAdded += async(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2, SocketReaction arg3) =>
            {
                StopMention(arg3.UserId);
            };
            Program.Client.UserIsTyping += async(Cacheable<IUser, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2) =>
            {
                StopMention(arg1.Id);
            };
            Program.Client.UserVoiceStateUpdated += async(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3) =>
            {
                StopMention(arg1.Id);
            };
            Program.Client.UserUpdated += async(SocketUser arg1, SocketUser arg2) =>
            {
                StopMention((arg1 ?? arg2).Id);
            };
        }

        public void AddMention(SocketTextChannel channel, 
            SocketGuildUser oper, 
            SocketGuildUser user, int duration)
        {
            var m = new MentionInfo()
            {
                Channel = channel,
                Starter = oper,
                Target = user,
                StartedAt = DateTime.Now,
                DurationSeconds = duration
            };
            Mentions[user.Id] = m;
            m.Start();
        }
    
        public void StopMention(ulong id)
        {
            if (Mentions.TryGetValue(id, out var m))
                m.CancelSource.Cancel();
        }
    }

    public class MentionInfo
    {
        public ITextChannel Channel { get; set; }
        public IGuildUser Starter { get; set; }
        public IGuildUser Target { get; set; }
        public DateTime StartedAt { get; set; }
        public int DurationSeconds { get; set; }

        public CancellationTokenSource CancelSource { get; private set; }

        async Task<int> mentionWithWebhook(DiscordWebhookClient wh, int count)
        {
            var ct = CancelSource.Token;
            await Task.Delay(count * 1000);
            int messages = 0;

            do
            {
                await wh.SendMessageAsync($"{Target.Mention}",
                    username: Starter.Nickname ?? Starter.Username,
                    avatarUrl: Starter.GetAnyAvatarUrl());
                messages++;
                await Task.Delay(1000);
            } while (!ct.IsCancellationRequested);
            Program.LogDebug($"M{count} has ended; {messages} sent", $"Mention-{Target.Id}");
            return messages;
        }

        async Task<int> mentionUser()
        {
            await Channel.SendMessageAsync($"Starting mention of {Target.Mention}");
            CancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(DurationSeconds));
            var whs = await Channel.GetWebhooksAsync();
            var webhooks = new[] {
                whs.FirstOrDefault(x => x.Name == "mention-0"),
                whs.FirstOrDefault(x => x.Name == "mention-1"),
                whs.FirstOrDefault(x => x.Name == "mention-2")
            };
            var tasks = new List<Task<int>>();
            for(int i = 0; i < 3; i++)
            {
                var wh = webhooks[i];
                if(wh == null)
                {
                    wh = await Channel.CreateWebhookAsync($"mention-{i}");
                }
                var client = new DiscordWebhookClient(wh);
                tasks.Add(mentionWithWebhook(client, i));
            }
            var result = await Task.WhenAll(tasks);
            var messages = result.Sum();
            await Channel.SendMessageAsync($"Mention of {Target.Mention} finished, total mentions: {messages + 2}");
            return messages;
        }

        public void Start()
        {
            Task.Run(mentionUser);
        }
    }
}
