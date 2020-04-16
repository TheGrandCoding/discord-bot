using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Commands
{
    public class BotCommandContext : SocketCommandContext
    {
        public BotUser BotUser { get; set; }

        public BotCommandContext(DiscordSocketClient client, SocketUserMessage msg) : base(client, msg)
        {
            if (!(msg.Author.IsBot || msg.Author.IsWebhook))
                BotUser = Program.GetUser(msg.Author);
        }
    }
}
