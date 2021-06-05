using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services.BuiltIn
{
    public class UserChangeService : Service
    {
        public event RoleAddedArgs RolesAdded;
        public event RoleRemovedArgs RolesRemoved;

        public override void OnReady()
        {
            Program.Client.GuildMemberUpdated += Client_GuildMemberUpdated;
        }

        private async System.Threading.Tasks.Task Client_GuildMemberUpdated(Cacheable<Discord.WebSocket.SocketGuildUser, ulong> cached1, Discord.WebSocket.SocketGuildUser arg2)
        {
            var arg1 = await cached1.GetOrDownloadAsync();
            var rolesRemoved = arg1.Roles.Where(x => arg2.Roles.Any(y => y.Id == x.Id) == false).ToArray();
            if (rolesRemoved.Length > 0)
                RolesRemoved?.Invoke(arg2, rolesRemoved);
            var rolesAdded = arg2.Roles.Where(x => arg1.Roles.Any(y => y.Id == x.Id) == false).ToArray();
            if (rolesAdded.Length > 0)
                RolesAdded?.Invoke(arg2, rolesAdded);
        }

        public delegate Task RoleAddedArgs(SocketGuildUser user, SocketRole[] added);
        public delegate Task RoleRemovedArgs(SocketGuildUser user, SocketRole[] removed);
    }
}
