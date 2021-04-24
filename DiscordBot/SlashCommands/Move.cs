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

namespace DiscordBot.SlashCommands
{
    [CommandGroup("move", "Move messages")]
    public class Move : BotSlashBase
    {
        public Move()
        {
            Webhooks = Program.Services.GetRequiredService<WebhookService>();
        }
        public WebhookService Webhooks { get; }

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
    
        [SlashCommand("last", "Moves the latest [amount] messages to the selected channel")]
        public async Task MoveBatch(int amount, SocketGuildChannel toChannel)
        {
            if (!(toChannel is SocketTextChannel to))
            {
                await Interaction.RespondAsync(":x: You must select a text channel.", type: InteractionResponseType.ChannelMessage, flags: InteractionResponseFlags.Ephemeral);
                return;
            }
            var from = Interaction.Channel;
            var fromPerms = Interaction.Member.GetPermissions(from);
            var toPerms = Interaction.Member.GetPermissions(to);
            if(!(toPerms.ManageMessages && fromPerms.ManageMessages))
            {
                await Interaction.RespondAsync(":x: You do not have permission to execute this command", type: InteractionResponseType.ChannelMessageWithSource, flags: InteractionResponseFlags.Ephemeral);
                return;
            }
            await Interaction.AcknowledgeAsync();
            var _m = await from.GetMessagesAsync((int)amount).FlattenAsync();
            foreach (var msg in _m.OrderBy(x => x.CreatedAt))
            {
                if (msg is IUserMessage umsg)
                {
                    await MoveMsg(umsg, to);
                }
            }
            await Interaction.FollowupAsync($"Moved {_m.Count()} messages.");
        }

        [SlashCommand("after", "Moves the provided message, and all that follow it, to the channel")]
        public async Task MoveAfter(
            [ParameterName("messageId")]string strMsgId, 
            SocketGuildChannel toChannel)
        {
            if(!(toChannel is SocketTextChannel to))
            {
                await Interaction.RespondAsync(":x: You must select a text channel.", type: InteractionResponseType.ChannelMessage, flags: InteractionResponseFlags.Ephemeral);
                return;
            }
            var from = Interaction.Channel;
            var fromPerms = Interaction.Member.GetPermissions(from);
            var toPerms = Interaction.Member.GetPermissions(to);
            if (!ulong.TryParse(strMsgId, out var messageId))
            {
                await Interaction.RespondAsync(":x: You must enter a message id - a long number.", type: InteractionResponseType.ChannelMessage, flags: InteractionResponseFlags.Ephemeral);
                return;
            }
            if (!(toPerms.ManageMessages && fromPerms.ManageMessages))
            {
                await Interaction.RespondAsync(":x: You do not have permission to execute this command", type: InteractionResponseType.ChannelMessageWithSource, flags: InteractionResponseFlags.Ephemeral);
                return;
            }
            var date = Discord.SnowflakeUtils.FromSnowflake(messageId);
            if(Math.Abs((DateTime.Now - date).TotalHours) > 2)
            {
                await Interaction.RespondAsync(":x: Message was sent too long ago.", type: InteractionResponseType.ChannelMessage, flags: InteractionResponseFlags.Ephemeral);
                return;
            }
            await Interaction.AcknowledgeAsync();

            // oldest   --> youngest
            // smallest --> largest; snowflake
            var messages = new List<IUserMessage>();
            
            IUserMessage last = null;
            do
            {
                IEnumerable<IMessage> gotten;
                if (last == null)
                    gotten = await from.GetMessagesAsync(5).FlattenAsync();
                else
                    gotten = await from.GetMessagesAsync(last, Direction.Before, 5).FlattenAsync();
                var ordered = gotten.OrderByDescending(x => x.Id);
                last = ordered.LastOrDefault(x => x is IUserMessage) as IUserMessage;
                foreach(var listMsg in ordered)
                {
                    if (!(listMsg is IUserMessage msg))
                        continue;
                    if(msg.CreatedAt < date)
                    {
                        if (msg.Id < messageId)
                            break;
                    }
                    messages.Add(msg);

                }

            } while(last == null || last.CreatedAt > date);
            if(messages.Count > 512)
            {
                await Interaction.FollowupAsync(":x: Too many messages to move.");
                return;
            }
            foreach(var msg in messages.OrderBy(x => x.Id))
            { // oldest first
                await MoveMsg(msg, to);
            }
            await Interaction.FollowupAsync($"Moved {messages.Count} messages.");
        }
    
    }
}
