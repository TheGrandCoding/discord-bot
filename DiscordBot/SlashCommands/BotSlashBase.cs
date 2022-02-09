using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.SlashCommands
{
    public abstract class BotSlashBase : Discord.Interactions.InteractionModuleBase<SocketInteractionContext>
    {
        private BotUser _user;
        public BotUser User
        {
            get
            {
                if(_user == null)
                    _user = Program.CreateUser(Context.Interaction.User);
                return _user;
            }
        }
    
    }
}
