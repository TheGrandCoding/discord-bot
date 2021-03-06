﻿using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.MLAPI;
using DiscordBot.Utils;
using Microsoft.EntityFrameworkCore.Internal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class EnsureLevelEliteness : SavedService
    {
        public Dictionary<ulong, GuildSave> Guilds { get; set; }


        public override void OnReady()
        {
            var sv = ReadSave();
            Guilds = Program.Deserialise<Dictionary<ulong, GuildSave>>(sv);
        }

        public async Task Catchup()
        {
            foreach(var keypair in Guilds)
            {
                var id = keypair.Key;
                var save = keypair.Value;
                var guild = Program.Client.GetGuild(id);
                foreach(var usr in guild.Users.Where(x => x.Roles.Any(y => y.Id == save.VerifyRole.Id)))
                {
                    var bUser = Program.GetUser(usr);
                    if (bUser.IsVerified)
                        continue;
                    await HandleNewLevel7(save, usr);
                }
            }
        }

        public override void OnLoaded()
        {
            Catchup().Wait();
            Program.Client.GuildMemberUpdated += async (Cacheable<SocketGuildUser, ulong> c1, SocketGuildUser arg2) =>
            {
                var arg1 = await c1.GetOrDownloadAsync();
                if(!Guilds.TryGetValue((arg1 ?? arg2).Guild.Id, out var save))
                    return;
                if (arg2.Roles.Any(x => x.Id == save.VerifyRole.Id))
                {
                    if(arg1.Roles.Any(x => x.Id == save.VerifyRole.Id) == false)
                    {
                        await HandleNewLevel7(save, arg2);
                    }
                }

            };
        }

        async Task<IUser> getAdmin(SocketGuildUser target, IRole role)
        {
            var log = await target.Guild.GetAuditLogsAsync(25, actionType: ActionType.MemberRoleUpdated)
                .FlattenAsync();
            foreach(var audit in log.OrderByDescending(x => x.CreatedAt))
            {
                var data = audit.Data as MemberRoleAuditLogData;
                if (data.Target.Id != target.Id)
                    continue;
                if (data.Roles.Any(x => x.Added && x.RoleId == role.Id))
                    return audit.User;
            }
            return null;
        }

        string getSession(BotUser user)
        {
            var existing = user.Tokens.FirstOrDefault(x => x.Name == AuthToken.HttpFullAccess);
            if(existing == null)
            {
                existing = new AuthToken(AuthToken.HttpFullAccess, 32);
                user.Tokens.Add(existing);
            }
            return existing.Value;
        }

        async Task HandleNewLevel7(GuildSave save, SocketGuildUser user)
        {
            var bUser = Program.GetUser(user);
            if (bUser.IsVerified)
                return;
            var responsible = await getAdmin(user, save.VerifyRole);
            var respStr = responsible?.Username ?? $"An administrator for `{user.Guild.Name}`";
            await user.RemoveRoleAsync(save.VerifyRole, new RequestOptions()
            {
                AuditLogReason = responsible == null ? "Role added only by vote" : $"User must be voted into that role"
            });
            if (responsible != null)
                save.Fails[responsible.Id] = save.Fails.GetValueOrDefault(responsible.Id, 0) + 1;
            bUser.IsApproved = true; // prevent them being locked from verifing
            var url = new UrlBuilder(Handler.LocalAPIUrl + "/verify");
            url.Add(AuthToken.SessionToken, getSession(bUser));
        }

        public override string GenerateSave()
            => Program.Serialise(Guilds ?? new Dictionary<ulong, GuildSave>());
    }

    public class GuildSave
    {
        [JsonProperty("r")]
        public IRole VerifyRole { get; set; }
        [JsonProperty("f")]
        public Dictionary<ulong, int> Fails { get; set; } = new Dictionary<ulong, int>();
    }
}

