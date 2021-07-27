using Discord;
using Discord.SlashCommands;
using Discord.WebSocket;
using DiscordBot.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [CommandGroup("purge", "Remove many messages at once")]
    [DefaultDisabled]
    public class Purge : BotSlashBase
    {

        async Task<IUserMessage> sendOrModify(IUserMessage message, string content)
        {
            if (message == null)
                return await Interaction.FollowupAsync(content, embeds: null) as IUserMessage;
            await message.ModifyAsync(x => x.Content = content);
            return message;
        }

        [SlashCommand("last", "Purges the last [count] messages, optionally only from the given user")]
        public async Task PurgeMessages([Required]int count,
            SocketGuildUser user = null)
        {
            IEnumerable<IMessage> messages;
            IMessage last = null;
            int done = 0;
            await Interaction.AcknowledgeAsync();
            IUserMessage response = null;
            var lastSent = DateTimeOffset.Now;
            var bulkDelete = new List<IMessage>();
            var manualDelete = new List<IMessage>();
            do
            {
                if (last == null)
                    messages = await Interaction.Channel.GetMessagesAsync().FlattenAsync();
                else
                    messages = await Interaction.Channel.GetMessagesAsync(last, Direction.Before).FlattenAsync();
                if (messages.Count() == 0)
                    break;
                last = messages.Last();
                foreach(var msg in messages.OrderByDescending(x => x.Id))
                { // we want to go newest (largest) to oldest (smallest), so descend
                    if(user != null)
                    {
                        if (msg.Author.Id != user.Id)
                            continue;
                    } else
                    {
                        if (msg.Author.Id == Program.AppInfo.Id)
                            continue;
                    }
                    var diff = DateTime.Now - msg.CreatedAt;
                    if (bulkDelete.Count < 100 && diff.TotalDays < 14) // two weeks 
                        bulkDelete.Add(msg);
                    else
                        manualDelete.Add(msg);
                    done++;
                    if (done >= count)
                        break;
                }
                if ((DateTime.Now - lastSent).TotalSeconds > 5)
                    response = await sendOrModify(response, $"Found {bulkDelete.Count + manualDelete.Count} messages to delete");
            } while (done < count);
            response = await sendOrModify(response, $"Removing {bulkDelete.Count + manualDelete.Count} messages");
            if (bulkDelete.Count > 0)
            {
                if(Interaction.Channel is ITextChannel txt)
                {
                    await bulkDelete.BulkDeleteAndTrackAsync(txt, $"bPurged by {Interaction.User.Mention}");
                } else
                { // can't bulk delete in other types of text channels, it seems
                    manualDelete.AddRange(bulkDelete);
                }
            }
            foreach(var msg in manualDelete)
            {
                await msg.DeleteAndTrackAsync($"Purged by {Interaction.User.Mention}");
            }
        }
    }
}
