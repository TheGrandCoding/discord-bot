using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services.Events;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Components
{
    public class GuildJoinModule : BotComponentBase
    {
        [ComponentInteraction("gjoin:*:*")]
        public async Task handleButton(string uId, string action)
        {
            var This = Program.Services.GetRequiredService<GuildJoinService>();
            ulong guildId = Context.Guild.Id;
            if (!ulong.TryParse(uId, out var userId))
                return;
            if (userId == Context.User.Id)
            {
                await Context.Interaction.RespondAsync(":x: You cannot interact with these buttons!", ephemeral: true, embeds: null);
                return;
            }
            if (!This.GuildData.TryGetValue(guildId, out var save))
                return;
            var guild = Program.Client.GetGuild(guildId);
            var user = guild.GetUser(userId);

            var invoker = (Context.User as SocketGuildUser);
            if (invoker == null)
                return;

            await Context.Interaction.DeferAsync();

            string alu = $"{invoker.Username} ({invoker.Id})";

            if (action == "kick")
            {
                if (invoker.GuildPermissions.KickMembers || invoker.GuildPermissions.Administrator)
                {
                    await user.KickAsync($"Kicked by {invoker.Username} ({invoker.Id}) via joinlog-buttons");
                    await Context.Interaction.UpdateAsync(x =>
                    {
                        x.Content = $"*User was kicked by {invoker.Mention}";
                        x.AllowedMentions = AllowedMentions.None;
                        x.Components = This.getButtonBuilder(user, save, true).Build();
                    });
                }
                else
                {
                    await Context.Interaction.FollowupAsync(":x: You do not have permission to kick this user", ephemeral: true, embeds: null);
                }
            }
            else if (action == "ban")
            {
                if (invoker.GuildPermissions.BanMembers || invoker.GuildPermissions.Administrator)
                {
                    await user.BanAsync(1, $"Banned by {alu} via joinlog-buttons");
                    await Context.Interaction.UpdateAsync(x =>
                    {
                        x.Content = $"*User was banned by {invoker.Mention}";
                        x.AllowedMentions = AllowedMentions.None;
                        x.Components = This.getButtonBuilder(user, save, true).Build();
                    });
                }
                else
                {
                    await Context.Interaction.FollowupAsync(":x: You do not have permission to ban this user", ephemeral: true, embeds: null);
                }
            }
            else
            {
                var roleId = ulong.Parse(action);
                var role = guild.GetRole(roleId);
                if (role == null)
                    return;

                if (!user.GuildPermissions.Administrator)
                {
                    var missing = role.Permissions.ToList().Where(x => !user.GuildPermissions.Has(x)).ToList();
                    if (missing.Count > 0)
                    {
                        await Context.Interaction.FollowupAsync(":x: You are missing the following permissions:\r\n- " + string.Join("\r\n- ", missing),
                            ephemeral: true, embeds: null);
                        return;
                    }
                }

                if (!user.Roles.Any(x => x.Id == roleId))
                {
                    await user.AddRoleAsync(roleId, new RequestOptions() { AuditLogReason = $"Given by joinlog-buttons, by {alu}" });
                }
                else
                {
                    await user.RemoveRoleAsync(roleId, new RequestOptions() { AuditLogReason = $"Taken by joinlog-buttons, by {alu}" });
                }
                await Context.Interaction.FollowupAsync($"Role {Discord.MentionUtils.MentionRole(roleId)} has been toggled",
                    ephemeral: true, allowedMentions: AllowedMentions.None, embeds: null);
            }

        }
    }
}
