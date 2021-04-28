using Discord.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    public class AdminCmds : BotSlashBase
    {
        [SlashCommand("aduty", "Toggles administrator role")]
        [DefaultDisabled]
        public async Task ToggleAdminDuty()
        {
            var adminRole = Interaction.Guild.Roles.FirstOrDefault(x => x.Name.StartsWith("Admin", StringComparison.OrdinalIgnoreCase) && x.Permissions.Administrator);
            if(adminRole == null)
            {
                await Interaction.RespondAsync(":x: No admin role setup for this guild.",
                    flags: Discord.InteractionResponseFlags.Ephemeral);
                return;
            }
            await Interaction.AcknowledgeAsync(Discord.InteractionResponseFlags.Ephemeral);
            if(Interaction.Member.Roles.Any(x => x.Id == adminRole.Id))
            {
                await Interaction.Member.RemoveRoleAsync(adminRole, new Discord.RequestOptions() { AuditLogReason = "Toggled admin duty" });
                await Interaction.FollowupAsync("Your admin rights have been revoked.");
            }
            else
            {
                await Interaction.Member.AddRoleAsync(adminRole, new Discord.RequestOptions() { AuditLogReason = "Toggled admin duty." });
                await Interaction.FollowupAsync("Your admin rights have been reinstated.");
            }
        } 
    }
}
