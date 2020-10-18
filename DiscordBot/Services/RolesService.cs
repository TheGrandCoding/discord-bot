using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Classes.Attributes;
using DiscordBot.Commands;
using DiscordBot.Permissions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Services
{
    [RequireService(typeof(ReactionService))]
    public class RolesService : SavedService
    {
        static RolesService instance { get; set; }
        public Dictionary<ulong, RolesSetup> Messages { get; set; }
        public ReactionService Service { get; set; }

        public static ITextChannel Inspection { get; set; }

        public PermissionsService Permissions { get; set; }

        public override string GenerateSave()
        {
            return Program.Serialise(Messages);
        }
        public override void OnReady()
        {
            instance = this;
            Service = Program.Services.GetRequiredService<ReactionService>();
            Permissions = Program.Services.GetRequiredService<PermissionsService>();
            Messages = Program.Deserialise<Dictionary<ulong, RolesSetup>>(ReadSave());
            Dictionary<string, string> permissions = new Dictionary<string, string>();
            foreach(var x in Messages)
            { // since it may be removed.
                var guild = Program.Client.GetGuild(x.Key);
                // in case reactions were added whilst bot offline
                Permissions.RegisterNewNode(new NodeInfo(
                    $"roles.{guild.Id}.*",
                    "Use all reaction roles for " + guild.Name)
                    .SetAssignedBy("roles.*"));
                foreach(var emoteStr in x.Value.Roles.Keys)
                {
                    IEmote emote;
                    if(Emote.TryParse(emoteStr, out var _e))
                    {
                        emote = _e;
                    } else
                    {
                        emote = new Emoji(emoteStr);
                    }
                    x.Value.Message.AddReactionAsync(emote).Wait();
                    var role = guild.GetRole(x.Value.Roles[emoteStr]);
                    var rolePerm = new NodeInfo(
                        $"roles.{guild.Id}.{role.Id}",
                        $"Use reaction to get/remove {role.Name} for {guild.Name}")
                        .SetAssignedBy($"roles.{guild.Id}.*");
                    Permissions.RegisterNewNode(rolePerm);
                }
            }
        }

        public override void OnLoaded()
        {
            OnDailyTick();
        }

        public override void OnDailyTick()
        {
            foreach (var x in Messages)
            { // since it may be removed.
                Service.Register(x.Value.Message, EventAction.Added | EventAction.Removed, handleReact, x.Key.ToString());
                var guild = Program.Client.GetGuild(x.Key);
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
                    var reacts = x.Value.Message.GetReactionUsersAsync(emote, 100).FlattenAsync().Result;
                    var rolePerm = Permissions.FindNode($"roles.{guild.Id}.{role.Id}");
                    foreach (var u in reacts)
                    {
                        var botUser = Program.GetUser(u);
                        if (PermChecker.UserHasPerm(botUser, rolePerm))
                        {
                            var gUser = guild.GetUser(u.Id);
                            if (!gUser.Roles.Any(x => x.Id == role.Id))
                            {
                                gUser.AddRoleAsync(role);
                                Program.LogMsg($"Catching up, adding '{role.Name}' to {gUser.Username}", LogSeverity.Info, "Roles");
                            }
                        }
                    }

                }
            }
        }

        public void Register(IGuild guild, IUserMessage message, Action<EmojiStore> action)
        {
            if(Messages.TryGetValue(guild.Id, out var prior))
            {
                try
                {
                    prior.Message.DeleteAsync().Wait();
                } catch { }
            }
            var store = new EmojiStore();
            action(store);
            Messages[guild.Id] = new RolesSetup()
            {
                Message = message,
                Roles = store,
            };
            Service.Register(message, EventAction.Added | EventAction.Removed, handleReact, guild.Id.ToString());
            Service.OnSave();
        }

        static async System.Threading.Tasks.Task<BotResult> runReactions(ReactionEventArgs e)
        {
            if (!(ulong.TryParse(e.State, out var guildId)))
                return new BotResult($"Failed to parse '{e.State}' as ulong.");
            if (!(instance.Messages.TryGetValue(guildId, out var setup)))
                return new BotResult($"Guild {guildId} has no reaction roles set up.");
            if (!(setup.Roles.TryGetValue(e.Emote, out var roleId)))
                return new BotResult($"Emote {e.Emote} does not have a corresponding role.");
            if (!(e.Message.Channel is ITextChannel txt))
                return new BotResult($"Current channel, {e.Message.Channel} is not a text channel");
            var role = txt.Guild.GetRole(roleId);
            if (role == null)
                return new BotResult($"Role {roleId} does not exist.");
            var perm = $"roles.{role.Guild.Id}.{role.Id}";
            var user = await txt.Guild.GetUserAsync(e.User.Id);
            var bUser = Program.GetUser(user);
            if (PermChecker.UserHasPerm(bUser, perm) == false)
            {
                await user.SendMessageAsync($":x: You do not have permission to use {e.Emote} to receive or remove the role \"{role.Name}\"");
                return new BotResult($"User {bUser.Name} lacks the `{perm}` permission.\r\n" +
                    $"Use `{Program.Prefix}perms viewo {bUser.Id}` to see what permissions they do have.");
            }
            if (e.Action == EventAction.Added)
                await user.AddRoleAsync(role);
            else
                await user.RemoveRoleAsync(role);
            return new BotResult("Successfully handled reaction.");
        }

        public static void handleReact(object sender, ReactionEventArgs e)
        {
            var result = runReactions(e).Result;
            if (Inspection != null)
            {
                var builder = new EmbedBuilder();
                builder.Title = "Reaction Role";
                builder.AddField("User", $"{e.User.Username}#{e.User.Discriminator}\r\n{e.User.Id}", true);
                builder.AddField("Emote", e.Emote, true);
                builder.Description = $"**{result.Reason}**";
                Inspection.SendMessageAsync(embed: builder.Build());
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
    }
}
