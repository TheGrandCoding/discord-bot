using Discord;
using DiscordBot.Classes.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Services
{
    [RequireService(typeof(ReactionService))]
    public class RolesService : SavedService
    {
        static RolesService instance { get; set; }
        public Dictionary<ulong, RolesSetup> Messages { get; set; }
        public ReactionService Service { get; set; }

        public override string GenerateSave()
        {
            return Program.Serialise(Messages);
        }
        public override void OnReady()
        {
            instance = this;
            Service = Program.Services.GetRequiredService<ReactionService>();
            Messages = Program.Deserialise<Dictionary<ulong, RolesSetup>>(ReadSave());
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
