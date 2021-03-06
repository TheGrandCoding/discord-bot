﻿using Discord.SlashCommands;
using Discord.WebSocket;
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
                    ephemeral: true, embeds: null);
                return;
            }
            await Interaction.AcknowledgeAsync(Discord.InteractionResponseFlags.Ephemeral);
            var member = Interaction.User as SocketGuildUser;
            if(member.Roles.Any(x => x.Id == adminRole.Id))
            {
                await member.RemoveRoleAsync(adminRole, new Discord.RequestOptions() { AuditLogReason = "Toggled admin duty" });
                await Interaction.FollowupAsync("Your admin rights have been revoked.", embeds: null);
            }
            else
            {
                await member.AddRoleAsync(adminRole, new Discord.RequestOptions() { AuditLogReason = "Toggled admin duty." });
                await Interaction.FollowupAsync("Your admin rights have been reinstated.", embeds: null);
            }
        } 
    }
}
