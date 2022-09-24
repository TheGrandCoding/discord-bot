using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Modules
{
    [Group("purge", "Remove many messages at once")]
    [DefaultMemberPermissions(GuildPermission.ManageMessages)]
    [DefaultPermission(false)]
    public class Purge : BotSlashBase
    {

        async Task<IUserMessage> sendOrModify(IUserMessage message, string content)
        {
            if (message == null)
                return await FollowupAsync(content, embeds: null) as IUserMessage;
            await message.ModifyAsync(x => x.Content = content);
            return message;
        }

        async Task _purge(IUser user, Func<IMessage, int, Task<IEnumerable<IMessage>>> fetcher)
        {
            IEnumerable<IMessage> messages = null;
            IMessage oldest = null;
            int done = 0;
            var bulkDelete = new List<IMessage>();
            var manualDelete = new List<IMessage>();
            var lastSent = DateTime.Now;
            IUserMessage response = null;
            do
            {
                messages = (await fetcher(oldest, done)).OrderByDescending(x => x.Id); // newest -> oldest
                if (messages.Count() == 0) break;
                oldest = messages.Last();
                foreach (var msg in messages.OrderByDescending(x => x.Id))
                { // we want to go newest (largest) to oldest (smallest), so descend
                    if (user != null)
                    {
                        if (msg.Author.Id != user.Id)
                            continue;
                    }
                    else
                    {
                        if (msg.Author.Id == Program.AppInfo.Id)
                            continue;
                    }
                    var diff = DateTime.Now - msg.CreatedAt;
                    if (diff.TotalDays < 14) // two weeks 
                        bulkDelete.Add(msg);
                    else
                        manualDelete.Add(msg);
                    done++;
                    if ((DateTime.Now - lastSent).TotalSeconds > 5)
                        response = await sendOrModify(response, $"Found {bulkDelete.Count + manualDelete.Count} messages to delete: {bulkDelete.Count} bulk, {manualDelete.Count} indiv.");
                }
            } while (messages.Count() > 0);
            if (Context.Channel is ITextChannel txt)
            {
                while (bulkDelete.Count > 0)
                {
                    var round = bulkDelete.Take(100);
                    bulkDelete = bulkDelete.Skip(100).ToList();
                    await round.BulkDeleteAndTrackAsync(txt, $"bPurged by {Context.User.Mention}");
                }
            }
            else
            {
                // can't bulk delete in other types of text channels, it seems
                manualDelete.AddRange(bulkDelete);
            }
            foreach (var msg in manualDelete)
            {
                await msg.DeleteAndTrackAsync($"Purged by {Context.User.Mention}");
            }
        }

        [SlashCommand("upto", "Purges messages up to the provided ID")]
        public async Task PurgeUntil(string messageId,
            SocketGuildUser user = null)
        {
            if(!ulong.TryParse(messageId, out var message))
            {
                await RespondAsync(":x: Invalid message ID.");
                return;
            }
            await DeferAsync();
            await _purge(user, async (IMessage last, int done) =>
            {
                IEnumerable<IMessage> messages;
                if (last == null)
                    messages = await Context.Channel.GetMessagesAsync().FlattenAsync();
                else
                    messages = await Context.Channel.GetMessagesAsync(last, Direction.Before).FlattenAsync();
                return messages.Where(x => x.Id >= message);
            });
        }

        [SlashCommand("last", "Purges the last [count] messages, optionally only from the given user")]
        public async Task PurgeMessages(int count,
            SocketGuildUser user = null)
        {
            await DeferAsync();
            
            await _purge(user, async (IMessage last, int done) =>
            {
                IEnumerable<IMessage> messages;
                if (last == null)
                    messages = await Context.Channel.GetMessagesAsync().FlattenAsync();
                else
                    messages = await Context.Channel.GetMessagesAsync(last, Direction.Before).FlattenAsync();
                var remainder = count - done;
                if (remainder <= 0) return new IMessage[0];
                return messages.OrderByDescending(x => x.Id).Take(remainder);
                    
            });


        }
    }
}
