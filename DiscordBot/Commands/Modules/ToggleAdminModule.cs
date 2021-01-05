using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    public class ToggleAdminModule : BotBase
    {
        async Task<RuntimeResult> toggle(SocketGuildUser user)
        {
            var admRole = Context.Guild.Roles.FirstOrDefault(x => x.Name == "Admin");
            if (user.Roles.Any(x => x.Id == admRole.Id))
            {
                await user.RemoveRoleAsync(admRole);
            } else if (user.Hierarchy >= admRole.Position)
            {
                await user.AddRoleAsync(admRole);
            } else
            {
                return new BotResult("You do not have permission to use this command");
            }
            return new BotResult();
        }
        [Command("aduty")]
        public async Task<RuntimeResult> ondmin()
        {
            var result = await toggle(Context.User as SocketGuildUser);
            if (result.IsSuccess)
                await ReplyAsync("Done.");
            return result;
        }
    }
}
