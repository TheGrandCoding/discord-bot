using Discord;
using Discord.SlashCommands;
using Discord.WebSocket;
using DiscordBot.Services;
using DiscordBot.Services.BuiltIn;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [CommandGroup("move", "Move messages")]
    [DefaultDisabled]
    public class Move : BotSlashBase
    {
        public Move()
        {
            Webhooks = Program.Services.GetRequiredService<WebhookService>();
        }
        public WebhookService Webhooks { get; }

        const int maximumMoveCount = 512;
        const int maximumHoursAgo = 72;

        private async Task MoveMsg(IUserMessage message, ITextChannel to, IThreadChannel thread)
        {
            var webhook = await Webhooks.GetWebhookClientAsync(to);
            var embeds = new List<Embed>();
            if (message.Embeds != null)
                foreach (var em in message.Embeds)
                    embeds.Add((Embed)em);
            string fileUrl = null;
            if (message.Attachments != null && message.Attachments.Count > 0)
            {
                var diffSeconds = (DateTimeOffset.Now - message.CreatedAt).TotalSeconds;
                var sleepFor = 10 - diffSeconds;
                if (sleepFor > 0)
                { // give logging thread time to download & upload attachment
                    Thread.Sleep((int)sleepFor * 1000);
                }
                var msgService = Program.Services.GetRequiredService<MsgService>();
                var newAttachment = await msgService.GetSavedAttachment(to.Guild, message.Id);
                if (newAttachment == null)
                {
                    var x = message.Attachments.First();
                    embeds.Add(new EmbedBuilder()
                        .WithTitle(x.Filename)
                        .WithUrl(x.Url)
                        .WithImageUrl(x.Url)
                        .WithFooter("Note: This file link may not work.")
                        .Build());
                }
                else
                {
                    fileUrl = newAttachment.Url;
                }
            }
            string content = message.Content;
            if (fileUrl != null)
            {
                if (string.IsNullOrWhiteSpace(content))
                    content = fileUrl;
                else
                    content += "\r\n" + fileUrl;
            }
            var opt = new RequestOptions()
            {
                CancelToken = Program.GetToken(),
                RetryMode = RetryMode.AlwaysRetry
            };
            await webhook.SendMessageAsync(
                content,
                false,
                embeds,
                (message.Author as IGuildUser).Nickname ?? message.Author.Username,
                message.Author.GetAnyAvatarUrl(), thread?.Id,
                options: opt
                );
            await message.DeleteAndTrackAsync("moving message", options: opt);
        }

        async Task<List<IUserMessage>> fetchMessages(ISocketMessageChannel from, int maxAmount, ulong fromId)
        {
            var messages = new List<IUserMessage>();
            IUserMessage last = null;
            do
            {
                IEnumerable<IMessage> gotten;
                if (last == null)
                    gotten = await from.GetMessagesAsync().FlattenAsync();
                else
                    gotten = await from.GetMessagesAsync(last, Direction.Before).FlattenAsync();
                var ordered = gotten.OrderByDescending(x => x.Id);
                last = ordered.LastOrDefault(x => x is IUserMessage) as IUserMessage;
                foreach (var listMsg in ordered)
                {
                    if (!(listMsg is IUserMessage msg))
                        continue;
                    if (msg.Id >= fromId)
                    {
                        if (msg.Author.Id == Program.AppInfo.Id)
                            continue;
                        messages.Add(msg);
                        if (messages.Count >= maxAmount)
                            break;
                    }
                }
                if (gotten.Count() < 100) // not a complete fetch, so we're at end of channel
                {
                    break;
                }
            } while (last == null || (last.Id >= fromId && messages.Count < maxAmount));
            return messages;
        }

        [SlashCommand("last", "Moves the latest [amount] messages to the selected channel")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task MoveBatch([Required] int amount, [Required] SocketGuildChannel chnl)
        {
            if (!(chnl is SocketTextChannel to))
            {
                await Interaction.RespondAsync(":x: You must select a text channel.",
                    ephemeral: true, embeds: null);
                return;
            }
            var from = Interaction.Channel as SocketTextChannel;
            var fromPerms = (Interaction.User as SocketGuildUser).GetPermissions(from);
            if (!fromPerms.ManageMessages)
            {
                await Interaction.RespondAsync($":x: You do not have permission to move messages to {to.Mention}", 
                    ephemeral: true);
                return;
            }
            await Interaction.DeferAsync();
            var date = DateTimeOffset.Now.AddHours(-maximumHoursAgo);
            var messages = await fetchMessages(from, Math.Min(amount, maximumMoveCount), Discord.SnowflakeUtils.ToSnowflake(date));
            var response = await Interaction.FollowupAsync($"Moving {messages.Count} messages.", embeds: null) as IUserMessage;
            DateTime last = DateTime.Now;
            int done = 0;
            foreach (var msg in messages.OrderBy(x => x.Id))
            { // oldest first
                await MoveMsg(msg, to, null);
                done++;
                if ((DateTime.Now - last).TotalSeconds > 5)
                {
                    last = DateTime.Now;
                    await response.ModifyAsync(x =>
                    {
                        x.Content = $"Moved {done}/{messages.Count} ({(done / messages.Count) * 100:00}%)";
                    });
                }
            }
            await response.ModifyAsync(x => x.Content = $"Moved {messages.Count} messages.");
        }

        async Task moveAfter(ulong messageId, ISocketMessageChannel from, ITextChannel to, IThreadChannel thread)
        {

            // oldest   --> youngest
            // smallest --> largest; snowflake
            var messages = await fetchMessages(from, maximumMoveCount * 2, messageId);
            if (messages.Count > maximumMoveCount)
            {
                await Interaction.ModifyOriginalResponseAsync(x => {
                    x.Content = ":x: Too many messages to move.";
                    });
                return;
            }
            //var response = await Interaction.FollowupAsync($"Moving {messages.Count} messages.", embeds: null) as IUserMessage;
            DateTime last = DateTime.Now;
            int done = 0;
            var membersAdded = new List<ulong>();
            await Interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = $"Moving {messages.Count} messages to {MentionUtils.MentionChannel(thread?.Id ?? to.Id)}";
            });
            foreach (var msg in messages.OrderBy(x => x.Id))
            { // oldest first
                if(thread != null)
                {
                    if (msg.Id == messageId)
                    {
                        membersAdded.Add(msg.Author.Id); // already in the thread
                        continue; // don't move the message that contains our parent thread.
                    }
                    if (msg.Author.IsBot == false && !membersAdded.Contains(msg.Author.Id))
                    {
                        await thread.AddMemberAsync(msg.Author.Id);
                        membersAdded.Add(msg.Author.Id);
                    }
                }
                await MoveMsg(msg, to, thread);
                done++;
                if ((DateTime.Now - last).TotalSeconds > 5)
                {
                    last = DateTime.Now;
                    await Interaction.ModifyOriginalResponseAsync(x =>
                    {
                        x.Content = $"Moved {done}/{messages.Count} ({(done / (double)messages.Count) * 100:00}%)";
                    });
                }
            }
            await Interaction.ModifyOriginalResponseAsync(x => x.Content = $"Moved {messages.Count} messages.");
        }

        [SlashCommand("after", "Moves the provided message, and all that follow it, to the channel")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task MoveAfter(
            [ParameterName("message")]
            [Required]string strMsgId,
            [Required] SocketGuildChannel chnl)
        {
            if (!(chnl is SocketTextChannel to))
            {
                await Interaction.RespondAsync(":x: You must select a text channel.", 
                    ephemeral: true, embeds: null);
                return;
            }
            var from = Interaction.Channel as SocketTextChannel;
            var fromPerms = (Interaction.User as SocketGuildUser).GetPermissions(from);
            if (!ulong.TryParse(strMsgId, out var messageId))
            {
                await Interaction.RespondAsync(":x: You must enter a message id - a long number.", 
                    ephemeral: true, embeds: null);
                return;
            }
            if (!fromPerms.ManageMessages)
            {
                await Interaction.RespondAsync($":x: You do not have permission to move mesages to {to.Mention}", 
                    ephemeral: true, embeds: null);
                return;
            }

            var date = Discord.SnowflakeUtils.FromSnowflake(messageId);
            if (Math.Abs((DateTime.Now - date).TotalHours) > maximumHoursAgo)
            {
                await Interaction.RespondAsync(":x: Message was sent too long ago.",
                    ephemeral: true, embeds: null);
                return;
            }

            await Interaction.DeferAsync();
            await moveAfter(messageId, from, to, null);

        }

        [SlashCommand("one", "Moves the single specified message to the selected channel")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task MoveOne([Required] string message, [Required]SocketGuildChannel chnl)
        {
            if (!(chnl is SocketTextChannel to))
            {
                await Interaction.RespondAsync(":x: You must select a text channel.", 
                    ephemeral: true, embeds: null);
                return;
            }
            var from = Interaction.Channel as ITextChannel;
            var fromPerms = (Interaction.User as SocketGuildUser).GetPermissions(from);
            if (!ulong.TryParse(message, out var messageId))
            {
                await Interaction.RespondAsync(":x: You must enter a message id - a long number.", 
                    ephemeral: true, embeds: null);
                return;
            }
            if (!fromPerms.ManageMessages)
            {
                await Interaction.RespondAsync($":x: You do not have permission to move mesages to {to.Mention}", 
                    ephemeral: true, embeds: null);
                return;
            }
            await Interaction.DeferAsync();
            var msg = await from.GetMessageAsync(messageId);
            if(msg == null || !(msg is IUserMessage umsg))
            {
                await Interaction.FollowupAsync(":x: That message does not exist", embeds: null);
                return;
            }
            await MoveMsg(umsg, to, null);
            await Interaction.FollowupAsync("Moved one message", embeds: null);
        }
    
    
        [SlashCommand("thread", "Moves all proceeding messages into a thread at the given message")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task MoveToThread(
            [ParameterName("message")]
            [Required]string strMsgId)
        {
            var from = Interaction.Channel as SocketTextChannel;
            var fromPerms = (Interaction.User as SocketGuildUser).GetPermissions(from);
            if (!ulong.TryParse(strMsgId, out var messageId))
            {
                await Interaction.RespondAsync(":x: You must enter a message id - a long number.",
                    ephemeral: true, embeds: null);
                return;
            }
            if (!fromPerms.ManageMessages)
            {
                await Interaction.RespondAsync($":x: You do not have permission to move mesages from this channel",
                    ephemeral: true, embeds: null);
                return;
            }

            var date = Discord.SnowflakeUtils.FromSnowflake(messageId);
            if (Math.Abs((DateTime.Now - date).TotalHours) > (maximumHoursAgo * 2))
            {
                await Interaction.RespondAsync(":x: Message was sent too long ago.",
                    ephemeral: true, embeds: null);
                return;
            }

            await Interaction.DeferAsync();
            var starterMessage = await from.GetMessageAsync(messageId);
            if (starterMessage == null)
            {
                await Interaction.RespondAsync(":x: Message does not exist",
                    ephemeral: true, embeds: null);
                return;
            }
            var thread = Program.Client.GetChannel(messageId) as IThreadChannel; // threads share same ID as their starter message
            if(thread == null)
            {
                thread = await from.CreateThread(messageId, x =>
                {
                    x.Name = Program.Clamp(starterMessage.Content, 100);
                    x.AutoArchiveDuration = 1440;
                });
            }
            await moveAfter(messageId, from, from, thread);
        }
    
    }
}
