using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Modules
{
    public class AdminCmds : BotSlashBase
    {
        [SlashCommand("aduty", "Toggles administrator role")]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [EnabledInDm(false)]
        public async Task ToggleAdminDuty()
        {
            var adminRole = (Context.Interaction.Channel as IGuildChannel).Guild.Roles.FirstOrDefault(x => x.Name.StartsWith("Admin", StringComparison.OrdinalIgnoreCase) && x.Permissions.Administrator);
            if(adminRole == null)
            {
                await RespondAsync(":x: No admin role setup for this guild.",
                    ephemeral: true, embeds: null);
                return;
            }
            await DeferAsync(true);
            var member = Context.Interaction.User as SocketGuildUser;
            if(member.Roles.Any(x => x.Id == adminRole.Id))
            {
                await member.RemoveRoleAsync(adminRole, new Discord.RequestOptions() { AuditLogReason = "Toggled admin duty" });
                await FollowupAsync("Your admin rights have been revoked.", embeds: null);
            }
            else
            {
                await member.AddRoleAsync(adminRole, new Discord.RequestOptions() { AuditLogReason = "Toggled admin duty." });
                await FollowupAsync("Your admin rights have been reinstated.", embeds: null);
            }
        } 
    }
}
