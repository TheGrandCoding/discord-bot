﻿using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Classes.Attributes;
using DiscordBot.Commands;
using DiscordBot.Interactions;
using DiscordBot.Permissions;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    //[RequireService(typeof(ReactionService))]
    public class RolesService : SavedService
    {
        static RolesService instance { get; set; }
        public Dictionary<ulong, RolesSetup> Messages { get; set; }

        public static ITextChannel Inspection { get; set; }

        public PermissionsService Permissions { get; set; }

        public override string GenerateSave()
        {
            return Program.Serialise(Messages);
        }

        public void RegisterPermissions()
        {
            foreach (var x in Messages)
            { // since it may be removed.
                var guild = Program.Client.GetGuild(x.Key);
                if(guild == null)
                {
                    Warning($"Could not find guild {x.Key}");
                    continue;
                }
                // in case reactions were added whilst bot offline
                if(!Permissions.AllNodes.ContainsKey($"roles.{guild.Id}.*"))
                {
                    Permissions.RegisterNewNode(new NodeInfo(
                        $"roles.{guild.Id}.*",
                        "Use all reaction roles for " + guild.Name)
                        .SetAssignedBy("roles.*"));
                }
                foreach (var emoteStr in x.Value.Roles.Keys)
                {
                    IEmote emote;
                    if (Emote.TryParse(emoteStr, out var _e))
                    {
                        emote = _e;
                    }
                    else
                    {
                        emote = new Emoji(emoteStr);
                    }
                    var role = guild.GetRole(x.Value.Roles[emoteStr]);
                    if(role == null)
                    {
                        Warning($"Could not find role {x.Value.Roles[emoteStr]} for guild {guild.Id}");
                        return;
                    }
                    var rolePerm = new NodeInfo(
                        $"roles.{guild.Id}.{role.Id}",
                        $"Use button to get/remove {role.Name} for {guild.Name}")
                        .SetAssignedBy($"roles.{guild.Id}.*");
                    if(!Permissions.AllNodes.ContainsKey(rolePerm.Node))
                    {
                        Permissions.RegisterNewNode(rolePerm);
                    }
                }
            }
        }

        public override void OnReady(IServiceProvider services)
        {
            instance = this;
            Permissions = Program.GlobalServices.GetRequiredService<PermissionsService>();
            Messages = Program.Deserialise<Dictionary<ulong, RolesSetup>>(ReadSave());
            RegisterPermissions();
        }

        public override void OnLoaded(IServiceProvider services)
        {
            OnDailyTick();
        }


        public void Register(IGuild guild, IUserMessage message, Action<EmojiStore> action)
        {
            if(Messages.TryGetValue(guild.Id, out var prior))
            {
                try
                {
                    prior.Message.DeleteAndTrackAsync("new registration message").Wait();
                } catch { }
            }
            var store = new EmojiStore();
            action(store);
            Messages[guild.Id] = new RolesSetup()
            {
                Message = message,
                Roles = store,
            };
        }

        static async System.Threading.Tasks.Task<BotResult> runReactions(string state, string rid, SocketMessageComponent e)
        {
            using var scope = Program.GlobalServices.CreateScope();
            if (!(ulong.TryParse(state, out var guildId)))
                return new BotResult($"Failed to parse '{state}' as ulong.");
            if (!(instance.Messages.TryGetValue(guildId, out var setup)))
                return new BotResult($"Guild {guildId} has no reaction roles set up.");
            if (!(ulong.TryParse(rid, out var roleId)))
                return new BotResult($"Failed to parse custom id as ulong: {rid}");
            if (!(e.Message.Channel is ITextChannel txt))
                return new BotResult($"Current channel, {e.Message.Channel} is not a text channel");
            var role = txt.Guild.GetRole(roleId);
            if (role == null)
                return new BotResult($"Role {roleId} does not exist.");
            var perm = $"roles.{role.Guild.Id}.{role.Id}";
            var user = await txt.Guild.GetUserAsync(e.User.Id);
            var db = scope.ServiceProvider.GetBotDb("RolesRunReacts");
            var result = await db.GetUserFromDiscord(user, true);
            if (!result.Success)
                return new BotResult($"Failed to fetch user from database.");
            if (PermChecker.UserHasPerm(result.Value, perm) == false)
            {
                return new BotResult($"You lack the permission to toggle that role.\r\n" /*+
                    $"Use `{Program.Prefix}perms viewo {bUser.Id}` to see what permissions they do have."*/);
            }
            if(user.RoleIds.Any(x => x == roleId))
            {
                await user.RemoveRoleAsync(role);
                await e.FollowupAsync($"You no longer have the {role.Name} role",
                    ephemeral: true, embeds: null);
            }
            else
            {
                await user.AddRoleAsync(role);
                await e.FollowupAsync($"You now have the {role.Name} role",
                    ephemeral: true, embeds: null);
            }
            return new BotResult();
        }

        public static async Task handleReact(string gId, string rId, SocketInteractionContext<SocketMessageComponent> e)
        {
            await e.Interaction.DeferAsync(true);
            var result = runReactions(gId, rId, e.Interaction).Result;
            if(!result.IsSuccess)
            {
                await e.Interaction.FollowupAsync($"Failed to change your role: {result.Reason}", ephemeral: true, embeds: null);
            }
            if (Inspection != null)
            {
                var builder = new EmbedBuilder();
                builder.Title = "Reaction Role";
                builder.AddField("User", $"{e.User.Username}#{e.User.Discriminator}\r\n{e.User.Id}", true);
                builder.AddField("Button", e.Interaction.Data.CustomId, true);
                builder.Description = $"**{result.Reason}**";
                await Inspection .SendMessageAsync(embed: builder.Build());
            }
        }

        public class RolesSetup
        {
            public IUserMessage Message { get; set; }
            public EmojiStore Roles { get; set; }
        }
        public class EmojiStore : Dictionary<string, ulong>
        {
            public ulong this[IEmote key]
            {
                get => this[key.ToString()];
                set => this[key.ToString()] = value;
            }
            public bool TryGetValue(IEmote key, out ulong roleId) => TryGetValue(key.ToString(), out roleId);
            public bool Remove(IEmote key) => Remove(key.ToString());
        }

        public class RolesComponents : BotComponentBase
        {
            [ComponentInteraction("roles:*:*")]
            public async Task handleReact(string gId, string rId)
            {
                await RolesService.handleReact(gId, rId, Context);
            }
        }
    }
}
