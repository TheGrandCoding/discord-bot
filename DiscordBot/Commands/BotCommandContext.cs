using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Permissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Commands
{
    public class BotCommandContext : SocketCommandContext
    {
        public BotDbUser BotDbUser { get; set; }

        public bool HasPerm(NodeInfo node)
        {
            if (BotDbUser == null)
                return false;
            return PermChecker.UserHasPerm(BotDbUser, node);
        }

        public BotCommandContext(DiscordSocketClient client, SocketUserMessage msg) : base(client, msg)
        {
            if (!(msg.Author.IsBot || msg.Author.IsWebhook))
                BotDbUser = BotDbContext.Get().GetUserFromDiscord(msg.Author, true).Result.Value;
        }
    }
}
