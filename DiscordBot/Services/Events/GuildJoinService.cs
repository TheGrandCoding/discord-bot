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

        public EmbedBuilder getAdminBuilder(SocketGuildUser user)
        {
            var builder = new EmbedBuilder();
            builder.Title = "User Joined";
            builder.WithAuthor(user);
            builder.AddField("Name", $"`{user.Username}#{user.Discriminator}`", true);
            builder.AddField("Id", $"`{user.Id}`", true);
            builder.AddField("Created On", $"{user.CreatedAt:u}\r\n" + Program.FormatTimeSpan(DateTime.UtcNow - user.CreatedAt.UtcDateTime), true);
            return builder;
        }

        public ComponentBuilder getButtonBuilder(SocketGuildUser user, GuildSave save, bool disabled = false)
        {
            var builder = new ComponentBuilder();
            var buttons = 0;

            foreach(var roleId in save.Roles)
            {
                var role = user.Guild.GetRole(roleId);
                if (role == null)
                    continue;
                builder.WithButton($"Add '{role.Name}'", $"gjoin:{user.Id}:{roleId}", ButtonStyle.Secondary, disabled: disabled, row: (buttons++) % 5);
            }

            var row = (buttons % 5) + 1;
            if (row > 5)
                row = 5;

            builder.WithButton(ButtonBuilder
                .CreateDangerButton("Kick User", $"gjoin:{user.Id}:kick")
                .WithDisabled(disabled), row);
            builder.WithButton(ButtonBuilder
                .CreateDangerButton("Ban User", $"gjoin:{user.Id}:ban")
                .WithDisabled(disabled), row);

            return builder;
        }

        private async Task UserJoined_Admin(Discord.WebSocket.SocketGuildUser arg)
        {
            if (!GuildData.TryGetValue(arg.Guild.Id, out var save))
                return;
            var embed = getAdminBuilder(arg).Build();
            var buttons = getButtonBuilder(arg, save).Build();

            await save.Channel.SendMessageAsync(embed: embed, components: buttons);
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
