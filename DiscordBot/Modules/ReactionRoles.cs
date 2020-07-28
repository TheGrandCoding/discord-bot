using Discord;
using Discord.Commands;
using DiscordBot.Commands;
using DiscordBot.Services;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    [Group("roles")]
    [Name("Reaction Roles")]
    public class ReactionRoles : BotModule
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
    }
}
