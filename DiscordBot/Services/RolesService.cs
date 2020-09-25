using Discord;
using DiscordBot.Classes;
using DiscordBot.Classes.Attributes;
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
                Service.Register(x.Value.Message, EventAction.Added | EventAction.Removed, handleReact, x.Key.ToString());
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
            foreach (var x in Messages)
            { // since it may be removed.
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
                                gUser.AddRoleAsync(role);
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

        public static void handleReact(object sender, ReactionEventArgs e)
        {
            if (!(ulong.TryParse(e.State, out var guildId)))
                return;
            if (!(instance.Messages.TryGetValue(guildId, out var setup)))
                return;
            if (!(setup.Roles.TryGetValue(e.Emote, out var roleId)))
                return;
            if (!(e.Message.Channel is ITextChannel txt))
                return;
            var role = txt.Guild.GetRole(roleId);
            if (role == null)
                return;
            var user = txt.Guild.GetUserAsync(e.User.Id).Result;
            if (e.Action == EventAction.Added)
                user.AddRoleAsync(role);
            else
                user.RemoveRoleAsync(role);
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
