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

        private async Task MoveMsg(IUserMessage message, ITextChannel to)
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
            await webhook.SendMessageAsync(
                content,
                false,
                embeds,
                (message.Author as IGuildUser).Nickname ?? message.Author.Username,
                message.Author.GetAnyAvatarUrl()
                );
            await message.DeleteAndTrackAsync("moving message");
        }

        async Task<List<IUserMessage>> fetchMessages(ITextChannel from, int maxAmount, ulong fromId)
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
                    ephemeral: true);
                return;
            }
            var from = Interaction.Channel as ITextChannel;
            var fromPerms = (Interaction.User as SocketGuildUser).GetPermissions(from);
            if (!fromPerms.ManageMessages)
            {
                await Interaction.RespondAsync($":x: You do not have permission to move messages to {to.Mention}", type: InteractionResponseType.ChannelMessageWithSource, 
                    ephemeral: true);
                return;
            }
            await Interaction.AcknowledgeAsync();
            var date = DateTimeOffset.Now.AddHours(-maximumHoursAgo);
            var messages = await fetchMessages(from, Math.Min(amount, maximumMoveCount), Discord.SnowflakeUtils.ToSnowflake(date));
            var response = await Interaction.FollowupAsync($"Moving {messages.Count} messages.") as IUserMessage;
            DateTime last = DateTime.Now;
            int done = 0;
            foreach (var msg in messages.OrderBy(x => x.Id))
            { // oldest first
                await MoveMsg(msg, to);
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
                    ephemeral: true);
                return;
            }
            var from = Interaction.Channel as ITextChannel;
            var fromPerms = (Interaction.User as SocketGuildUser).GetPermissions(from);
            if (!ulong.TryParse(strMsgId, out var messageId))
            {
                await Interaction.RespondAsync(":x: You must enter a message id - a long number.", 
                    ephemeral: true);
                return;
            }
            if (!fromPerms.ManageMessages)
            {
                await Interaction.RespondAsync($":x: You do not have permission to move mesages to {to.Mention}", 
                    ephemeral: true);
                return;
            }
            var date = Discord.SnowflakeUtils.FromSnowflake(messageId);
            if (Math.Abs((DateTime.Now - date).TotalHours) > maximumHoursAgo)
            {
                await Interaction.RespondAsync(":x: Message was sent too long ago.", 
                    ephemeral: true);
                return;
            }
            await Interaction.AcknowledgeAsync();

            // oldest   --> youngest
            // smallest --> largest; snowflake
            var messages = await fetchMessages(from, maximumMoveCount * 2, messageId);
            if (messages.Count > maximumMoveCount)
            {
                await Interaction.FollowupAsync(":x: Too many messages to move.");
                return;
            }
            var response = await Interaction.FollowupAsync($"Moving {messages.Count} messages.") as IUserMessage;
            DateTime last = DateTime.Now;
            int done = 0;
            foreach (var msg in messages.OrderBy(x => x.Id))
            { // oldest first
                await MoveMsg(msg, to);
                done++;
                if ((DateTime.Now - last).TotalSeconds > 5)
                {
                    last = DateTime.Now;
                    await response.ModifyAsync(x =>
                    {
                        x.Content = $"Moved {done}/{messages.Count} ({(done / (double)messages.Count) * 100:00}%)";
                    });
                }
            }
            await response.ModifyAsync(x => x.Content = $"Moved {messages.Count} messages.");
        }

        [SlashCommand("one", "Moves the single specified message to the selected channel")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task MoveOne([Required] string message, [Required]SocketGuildChannel chnl)
        {
            if (!(chnl is SocketTextChannel to))
            {
                await Interaction.RespondAsync(":x: You must select a text channel.", 
                    ephemeral: true);
                return;
            }
            var from = Interaction.Channel as ITextChannel;
            var fromPerms = (Interaction.User as SocketGuildUser).GetPermissions(from);
            if (!ulong.TryParse(message, out var messageId))
            {
                await Interaction.RespondAsync(":x: You must enter a message id - a long number.", 
                    ephemeral: true);
                return;
            }
            if (!fromPerms.ManageMessages)
            {
                await Interaction.RespondAsync($":x: You do not have permission to move mesages to {to.Mention}", 
                    ephemeral: true);
                return;
            }
            await Interaction.AcknowledgeAsync();
            var msg = await from.GetMessageAsync(messageId);
            if(msg == null || !(msg is IUserMessage umsg))
            {
                await Interaction.FollowupAsync(":x: That message does not exist");
                return;
            }
            await MoveMsg(umsg, to);
            await Interaction.FollowupAsync("Moved one message");
        }
    }
}
