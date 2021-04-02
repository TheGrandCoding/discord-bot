using Discord;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
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
    }
}
