using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class MuteService : Service
    {
        public override void OnReady()
        {
            Program.Client.MessageReceived += Client_MessageReceived;
        }

        private async Task Client_MessageReceived(Discord.WebSocket.SocketMessage arg)
        {
            if (!(arg is IUserMessage um))
                return;
            if (!(um.Author is SocketGuildUser gUser))
                return;
            if (!(um.Channel is ITextChannel tchnl))
                return;
            if (gUser.GuildPermissions.Administrator)
                return;
            if (tchnl.Name == "muted")
                return;
            var muted = gUser.Roles.FirstOrDefault(x => x.Name == "Muted");
            if (muted == null)
                return;
            await tchnl.AddPermissionOverwriteAsync(muted, new OverwritePermissions(sendMessages: PermValue.Deny));
            await arg.DeleteAsync(new RequestOptions()
            {
                AuditLogReason = "User is Muted"
            });
        }
    }
}
