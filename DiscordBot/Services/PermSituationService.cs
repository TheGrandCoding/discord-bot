using Discord;
using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Services
{
    public class PermSituationService : SavedService
    {
        public Dictionary<ulong, Actions> Situations { get; set; } = new Dictionary<ulong, Actions>();

        public override string GenerateSave()
        {
            return Program.Serialise(Situations);
        }

        public void Register(IGuild guild, string e, string added, string removed, ulong? state)
        {
            var sit = new Situation()
            {
                Event = e,
                PermAdded = added,
                PermRemoved = removed,
                State = state
            };
            if (Situations.TryGetValue(guild.Id, out var ls))
                ls[e] = sit;
            else
                Situations[guild.Id] = new Actions() { { e, sit } };
        }

        public override void OnReady()
        {
            Situations = Program.Deserialise<Dictionary<ulong, Actions>>(ReadSave());
            Program.Client.UserJoined += Client_UserJoined;
            Program.Client.GuildMemberUpdated += Client_GuildMemberUpdated;
        }

        void perform(BotDbUser user, ulong id, string e, ulong? criteria)
        {
            if(Situations.TryGetValue(id, out var actions) && actions.TryGetValue(e, out var sit))
            {
                if (sit.State != null && sit.State != criteria)
                    return;
                if(!string.IsNullOrWhiteSpace(sit.PermAdded))
                    user.WithPerm(Perm.Parse(sit.PermAdded));
                if(!string.IsNullOrWhiteSpace(sit.PermRemoved))
                    user.Permissions.RemoveAll(x => x.PermNode.RawNode == sit.PermRemoved);
            }
        }

        private async System.Threading.Tasks.Task Client_UserJoined(Discord.WebSocket.SocketGuildUser arg)
        {
            var db = BotDbContext.Get();
            perform((await db.GetUserFromDiscord(arg, true)).Value, arg.Guild.Id, "UserJoined", null);
        }

        private async System.Threading.Tasks.Task Client_GuildMemberUpdated(Cacheable<Discord.WebSocket.SocketGuildUser, ulong> cached, Discord.WebSocket.SocketGuildUser arg2)
        {
            if (!cached.HasValue)
                return;
            var arg1 = cached.Value;
            var priorRoles = arg1?.Roles.Select(x => x.Id).ToList() ?? new List<ulong>();
            var currentRoles = arg2?.Roles.Select(x => x.Id).ToList() ?? new List<ulong>();

            var removedRoles = priorRoles.Where(x => currentRoles.Contains(x) == false);
            var addedRoles = currentRoles.Where(x => priorRoles.Contains(x) == false);

            var db = BotDbContext.Get();
            var bUser = (await db.GetUserFromDiscord(arg2 ?? arg1, true)).Value;

            foreach(var role in removedRoles)
            {
                perform(bUser, arg2.Guild.Id, "RoleRemoved", role);
            }
            foreach (var role in addedRoles)
            {
                perform(bUser, arg2.Guild.Id, "RoleAdded", role);
            }
        }
    }

    public class Actions : Dictionary<string, Situation>
    {
        public Situation RoleAdded
        {
            get => this["RoleAdded"];
            set => this["RoleAdded"] = value;
        }
        public Situation RoleRemoved
        {
            get => this["RoleRemoved"];
            set => this["RoleRemoved"] = value;
        }
        public Situation UserJoined
        {
            get => this["UserJoined"];
            set => this["UserJoined"] = value;
        }

    }

    public class Situation
    {
        public string Event { get; set; }
        public ulong? State { get; set; }
        public string PermAdded { get; set; }
        public string PermRemoved { get; set; }
    }
}
