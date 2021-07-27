using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services.Events
{
    public class GuildJoinService : SavedService
    {
        public ConcurrentDictionary<ulong, GuildSave> GuildData { get; set; } = new ConcurrentDictionary<ulong, GuildSave>();

        public MessageComponentService Service { get; set; }

        public override string GenerateSave()
            => Program.Serialise(GuildData);

        public override void OnLoaded()
        {
            var sv = ReadSave();
            GuildData = Program.Deserialise<ConcurrentDictionary<ulong, GuildSave>>(sv);
            Program.Client.UserJoined += UserJoined_Admin;

        }

        public class GuildSave
        {
            public List<ulong> Roles { get; set; } = new List<ulong>();
            public ITextChannel Channel { get; set; }
        }

        EmbedBuilder getAdminBuilder(SocketGuildUser user)
        {
            var builder = new EmbedBuilder();
            builder.Title = "User Joined";
            builder.WithAuthor(user);
            builder.AddField("Name", $"`{user.Username}#{user.Discriminator}`", true);
            builder.AddField("Id", $"`{user.Id}`", true);
            builder.AddField("Created On", $"{user.CreatedAt:u}\r\n" + Program.FormatTimeSpan(DateTime.UtcNow - user.CreatedAt.UtcDateTime), true);
            return builder;
        }

        ComponentBuilder getButtonBuilder(SocketGuildUser user, GuildSave save, bool disabled = false)
        {
            var builder = new ComponentBuilder();
            var buttons = 0;

            foreach(var roleId in save.Roles)
            {
                var role = user.Guild.GetRole(roleId);
                if (role == null)
                    continue;
                builder.WithButton($"Add '{role.Name}'", $"{roleId}", ButtonStyle.Secondary, disabled: disabled, row: (buttons++) % 5);
            }

            var row = (buttons % 5) + 1;
            if (row > 5)
                row = 5;

            builder.WithButton(ButtonBuilder
                .CreateDangerButton("Kick User", "kick")
                .WithDisabled(disabled), row);
            builder.WithButton(ButtonBuilder
                .CreateDangerButton("Ban User", "ban")
                .WithDisabled(disabled), row);

            return builder;
        }

        private async Task UserJoined_Admin(Discord.WebSocket.SocketGuildUser arg)
        {
            if (!GuildData.TryGetValue(arg.Guild.Id, out var save))
                return;
            var embed = getAdminBuilder(arg).Build();
            var buttons = getButtonBuilder(arg, save).Build();

            var msg = await save.Channel.SendMessageAsync(embed: embed, component: buttons);

            Service.Register(msg, handleAdminButton, $"{arg.Guild.Id}-{arg.Id}");
        }

        public static async Task handleAdminButton(CallbackEventArgs e)
        {
            var This = Program.Services.GetRequiredService<GuildJoinService>();
            var split = e.State.Split('-');
            if (!ulong.TryParse(split[0], out var guildId))
                return;
            if (!ulong.TryParse(split[1], out var userId))
                return;
            if(userId == e.User.Id)
            {
                await e.Interaction.RespondAsync(":x: You cannot interact with these buttons!", ephemeral: true, embeds: null);
                return;
            }
            if (!This.GuildData.TryGetValue(guildId, out var save))
                return;
            var guild = Program.Client.GetGuild(guildId);
            var user = guild.GetUser(userId);

            var invoker = (e.User as SocketGuildUser);
            if (invoker == null)
                return;

            await e.Interaction.AcknowledgeAsync();

            string alu = $"{invoker.Username} ({invoker.Id})";

            if(e.ComponentId == "kick")
            {
                if(invoker.GuildPermissions.KickMembers || invoker.GuildPermissions.Administrator)
                {
                    await user.KickAsync($"Kicked by {invoker.Username} ({invoker.Id}) via joinlog-buttons");
                    await e.Message.ModifyAsync(x =>
                    {
                        x.Content = $"*User was kicked by {invoker.Mention}";
                        x.AllowedMentions = AllowedMentions.None;
                        x.Components = This.getButtonBuilder(user, save, true).Build();
                    });
                } else
                {
                    await e.Interaction.FollowupAsync(":x: You do not have permission to kick this user", ephemeral: true, embeds: null);
                }
            } else if (e.ComponentId == "ban")
            {
                if (invoker.GuildPermissions.BanMembers || invoker.GuildPermissions.Administrator)
                {
                    await user.BanAsync(1, $"Banned by {alu} via joinlog-buttons");
                    await e.Message.ModifyAsync(x =>
                    {
                        x.Content = $"*User was banned by {invoker.Mention}";
                        x.AllowedMentions = AllowedMentions.None;
                        x.Components = This.getButtonBuilder(user, save, true).Build();
                    });
                }
                else
                {
                    await e.Interaction.FollowupAsync(":x: You do not have permission to ban this user", ephemeral: true, embeds: null);
                }
            } else
            {
                var roleId = ulong.Parse(e.ComponentId);
                var role = guild.GetRole(roleId);
                if (role == null)
                    return;

                if(!user.GuildPermissions.Administrator)
                {
                    var missing = role.Permissions.ToList().Where(x => !user.GuildPermissions.Has(x)).ToList();
                    if (missing.Count > 0)
                    {
                        await e.Interaction.FollowupAsync(":x: You are missing the following permissions:\r\n- " + string.Join("\r\n- ", missing),
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
                await e.Interaction.FollowupAsync($"Role {Discord.MentionUtils.MentionRole(roleId)} has been toggled", 
                    ephemeral: true, allowedMentions: AllowedMentions.None, embeds: null);
            }

        }
    }

    [Name("GuildJoin Commands")]
    [Group("guildjoin"), Alias("gj")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public class GuildJoinCommands : BotBase
    {
        public GuildJoinService Service { get; set; }

        [Command("role add"), Alias("add role")]
        [Summary("Adds a role to the buttons for quick selection")]
        public async Task AddRole(SocketRole role)
        {
            if(!Service.GuildData.TryGetValue(Context.Guild.Id, out var save))
            {
                save = new GuildJoinService.GuildSave();
                Service.GuildData[Context.Guild.Id] = save;
            }
            save.Roles.RemoveAll(x => x == role.Id);
            save.Roles.Add(role.Id);
            Service.OnSave();
            await Success("Added role, new buttons will have this role displayed");
        }

        [Command("role remove"), Alias("remove role")]
        [Summary("Removes a role to the buttons for quick selection")]
        public async Task RemoveRole(SocketRole role)
        {
            if (!Service.GuildData.TryGetValue(Context.Guild.Id, out var save))
            {
                save = new GuildJoinService.GuildSave();
                Service.GuildData[Context.Guild.Id] = save;
            }
            save.Roles.RemoveAll(x => x == role.Id);
            Service.OnSave();
            await Success("Removed role, new buttons will no longer have this role displayed");
        }

        [Command("admin channel"), Alias("ac")]
        [Summary("Sets current channel as the admin channel")]
        public async Task SetChannel()
        {
            if (!Service.GuildData.TryGetValue(Context.Guild.Id, out var save))
            {
                save = new GuildJoinService.GuildSave();
                Service.GuildData[Context.Guild.Id] = save;
            }
            save.Channel = Context.Channel as ITextChannel;
            Service.OnSave();
            await Success("Set admin channel as " + (save.Channel?.Mention ?? "<error>"));
        }

    }

}
