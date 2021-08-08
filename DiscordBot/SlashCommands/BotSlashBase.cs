using Discord;
using Discord.Commands.SlashCommands.Types;
using Discord.SlashCommands;
using Discord.WebSocket;
using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.SlashCommands
{
    public abstract class BotSlashBase : SlashCommandModule<SocketInteraction>, ISlashCommandModule
    {
        private BotUser _user;
        public BotUser User
        {
            get
            {
                if(_user == null)
                    _user = Program.GetUser(Interaction.User);
                return _user;
            }
        }
    
    }
}
