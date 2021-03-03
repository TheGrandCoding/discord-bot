using Discord;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class FunService : Service
    {
        public override void OnReady()
        {
            Program.Client.MessageReceived += Client_MessageReceived;
        }

        private async Task Client_MessageReceived(Discord.WebSocket.SocketMessage arg)
        {
            if (arg.Author.IsBot)
                return;
            if(arg.Content == "well")
            {
                await arg.Channel.SendMessageAsync("https://pruittwater.com/wp-content/uploads/2018/04/Deep-Well-Ocala-Fl-1024x768.jpg",
                    messageReference: new MessageReference(arg.Id, arg.Channel.Id));
            }
        }
    }
}
