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
        public BotUser BotUser { get; set; }

        public bool HasPerm(NodeInfo node)
        {
            if (BotUser == null)
                return false;
            return PermChecker.UserHasPerm(BotUser, node);
        }

        public BotCommandContext(DiscordSocketClient client, SocketUserMessage msg) : base(client, msg)
        {
            if (!(msg.Author.IsBot || msg.Author.IsWebhook))
                BotUser = Program.CreateUser(msg.Author);
        }
    }
}
