using Discord;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Utils
{
    public static class MessageUtils
    {
        /// <summary>
        /// Deletes a message and keeps track such that the <see cref="DiscordBot.Services.LoggingService"/>
        /// knows that the bot deleted the message
        /// </summary>
        /// <param name="message">The message to be deleted</param>
        /// <returns></returns>
        public static async Task DeleteAndTrackAsync(this IMessage message, string reason, RequestOptions options = null) 
        {
            await message.DeleteAsync(options);
            LoggingService.MessagesDeletedByBot[message.Id] = reason;
        }

        public static async Task BulkDeleteAndTrackAsync(this IEnumerable<IMessage> messages, ITextChannel channel, string reason, RequestOptions options = null)
        {
            var ids = messages.Select(x => x.Id);
            foreach (var id in ids)
                LoggingService.MessagesDeletedByBot[id] = reason;
            try
            {
                if (options == null)
                    options = new RequestOptions()
                    {
                        AuditLogReason = reason
                    };
                await channel.DeleteMessagesAsync(ids, options);
            } catch
            {
                foreach (var id in ids) // atomic
                    LoggingService.MessagesDeletedByBot.Remove(id);
                throw;
            }

        }
    }
}
