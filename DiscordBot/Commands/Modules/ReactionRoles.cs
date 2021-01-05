using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Commands;
using DiscordBot.Services;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("roles")]
    [Name("Reaction Roles")]
    [RequireUserPermission(GuildPermission.ManageRoles)]
    public class ReactionRoles : BotBase
    {
        public RolesService Service { get; set; }

        [Command("start")]
        [Alias("register")]
        [Summary("Sends a message for reaction roles to be added to")]
        public async Task Register()
        {
            var msg = await ReplyAsync(embed: new EmbedBuilder()
                .WithTitle("Reaction Roles")
                .WithDescription($"Use `{Program.Prefix}roles add [emote] [@role]` to add pairs")
                .Build());
            Service.Register(Context.Guild, msg, x => { });
            await Context.Message.DeleteAsync();
        }
        [Command("add")]
        [Summary("Adds a new emote-role pair to the server")]
        public async Task<RuntimeResult> AddRole(IEmote emote, IRole role)
        {
            if(!Service.Messages.TryGetValue(Context.Guild.Id, out var setup))
                return new BotResult($"You must run `{Program.Prefix}roles register` in the desired channel first");
            if (role.Position >= Context.Guild.CurrentUser.Hierarchy)
                return new BotResult($"That role is above my highest, so I would be unable to assign it.");
            setup.Roles[emote] = role.Id;
            await setup.Message.AddReactionAsync(emote);
            var builder = new EmbedBuilder();
            builder.Title = "Reaction Roles";
            foreach (var pair in setup.Roles)
                builder.AddField(pair.Key, $"<@&{pair.Value}>", true);
            await setup.Message.ModifyAsync(x =>
            {
                x.Embed = builder.Build();
            });
            Service.OnSave();
            return new BotResult();
        }

        [Command("list")]
        [Summary("Lists to the roles current")]
        public async Task<RuntimeResult> ListRoles()
        {
            if (!Service.Messages.TryGetValue(Context.Guild.Id, out var setup))
                return new BotResult($"There are no roles; you must run `{Program.Prefix}roles register` in the desired channel first");
            var builder = new EmbedBuilder();
            builder.Description = $"[Link to Message]({setup.Message.GetJumpUrl()})";
            await ReplyAsync(embed: builder.Build());
            return new BotResult();
        }

        [Command("inspect")]
        [Summary("Inspects a role interaction")]
        public async Task Inspect()
        {
            if(RolesService.Inspection == null)
            {
                RolesService.Inspection = (ITextChannel)Context.Channel;
                await ReplyAsync("Inspecting roles.");
            } else
            {
                RolesService.Inspection = null;
                await ReplyAsync("No longer inspecting roles.");
            }
        }

        [Command("remove")]
        [Summary("Removes the emote-role pair.")]
        public async Task<RuntimeResult> RemoveRole(IEmote emote)
        {
            if(!Service.Messages.TryGetValue(Context.Guild.Id, out var setup))
                return new BotResult($"You must run `{Program.Prefix}roles register` in the desired channel first");
            if (!setup.Roles.TryGetValue(emote, out _))
                return new BotResult("That emote has not been assigned to any roles");
            setup.Roles.Remove(emote);
            await setup.Message.RemoveReactionAsync(emote, Program.Client.CurrentUser);
            var builder = new EmbedBuilder();
            builder.Title = "Reaction Roles";
            foreach (var pair in setup.Roles)
                builder.AddField(pair.Key, $"<@&{pair.Value}>", true);
            await setup.Message.ModifyAsync(x =>
            {
                x.Embed = builder.Build();
            });
            Service.OnSave();
            return new BotResult();
        }

        [Command("block")]
        [Summary("Prevents a user from performing any reaction roles in this server")]
        public async Task BlockWhole(SocketGuildUser user)
        {
            var bUser = Program.GetUser(user);
            var perm = Perm.Parse($"-roles.{user.Guild.Id}.*");
            if(bUser.Permissions.RemoveAll(x => x.RawNode == perm.RawNode) > 0)
            {
                await ReplyAsync("Unblocked");
            } else
            {
                bUser.Permissions.Add(perm);
                await ReplyAsync("Blocked.");
            }
        }

        [Command("block")]
        [Summary("Prevents a user from getting the specific role via reaction in this server")]
        public async Task BlockWhole(SocketGuildUser user, IRole role)
        {
            var bUser = Program.GetUser(user);
            var perm = Perm.Parse($"-roles.{user.Guild.Id}.{role.Id}");
            if (bUser.Permissions.RemoveAll(x => x.RawNode == perm.RawNode) > 0)
            {
                await ReplyAsync("Unblocked");
            }
            else
            {
                bUser.Permissions.Add(perm);
                await ReplyAsync("Blocked.");
            }
        }
    }
}
