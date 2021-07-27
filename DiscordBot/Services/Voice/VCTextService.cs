using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Classes.Attributes;
using DiscordBot.Services.Games;
using DiscordBot.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class VCTextService : SavedService
    {
        public ConcurrentDictionary<SocketVoiceChannel, syncSave> Pairings { get; set; } = new ConcurrentDictionary<SocketVoiceChannel, syncSave>();

        public override string GenerateSave()
        {
            var dict = new Dictionary<ulong, syncSave>();
            var t = Pairings;
            foreach (var keypair in t)
                dict[keypair.Key.Id] = keypair.Value;
            return Program.Serialise(dict);
        }

        public override void OnLoaded()
        {
            var sv = Program.Deserialise<Dictionary<ulong, syncSave>>(ReadSave());
            Pairings = new ConcurrentDictionary<SocketVoiceChannel, syncSave>();
            foreach (var keypair in sv)
                Pairings[Program.Client.GetChannel(keypair.Key) as SocketVoiceChannel] = keypair.Value;
            catchup().Wait();
            Program.Client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
        }

        bool hasEnabledPairing(SocketGuildUser user, bool muted, BotUser bUser = null)
        {
            bUser ??= Program.GetUser(user);
            if (bUser.Options.PairedVoiceChannels == CreateChannelForVoice.Never)
                return false;
            if (bUser.Options.PairedVoiceChannels == CreateChannelForVoice.WhenMuted && muted == false)
                return false;
            return true;
        }

        private async Task Client_UserVoiceStateUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
        {
            if (!(arg1 is SocketGuildUser user))
                return;
            if (TTTGame.IsTTTVoice(arg2.VoiceChannel) || TTTGame.IsTTTVoice(arg3.VoiceChannel))
                return;
            if (user.IsBot)
                return;
            if (arg3.VoiceChannel == null)
                await UserLeftVc(user, arg2);
            else if (arg2.VoiceChannel == null)
                await UserJoinedVc(user, arg3);
            else if (arg2.VoiceChannel.Id != arg3.VoiceChannel.Id)
                await UserMovedVc(user, arg2, arg3);
        }

        OverwritePermissions perms(bool manage)
        {
            return new OverwritePermissions(
                viewChannel: PermValue.Allow,
                manageChannel: manage ? PermValue.Allow : PermValue.Inherit
                );
        }

        const string feature = "PRIVATE_THREADS";
        async Task UserJoinedVc(SocketGuildUser user, SocketVoiceState state)
        {
            var voice = state.VoiceChannel;
            syncSave save;
            bool manage = false;
            if(Pairings.TryGetValue(voice, out save))
            {
            } else if(hasEnabledPairing(user, state.IsSelfMuted))
            {
                manage = true;
                var ls = new List<Overwrite>();
                ls.Add(new Overwrite(user.Guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)));
                foreach(var x in voice.Users)
                {
                    ls.Add(new Overwrite(x.Id, PermissionTarget.User, perms(x.Id == user.Id)));
                }
                save = new syncSave();
                save.TextChannel = await user.Guild.CreateTextChannelAsync("pair-" + voice.Name, x =>
                {
                    x.PermissionOverwrites = ls;
                    x.CategoryId = voice.CategoryId;
                    x.Position = 999;
                    x.Topic = $"Paired channel with <#{voice.Id}>.";
                });
                Pairings[voice] = save;
                var starterMessage = await save.TextChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithTitle("Paired Channel")
                    .WithDescription("This channel will be deleted once the user leaves the paired voice channel.")
                    .WithAuthor(user)
                    .Build());

                if(voice.Guild.Features.Contains(feature))
                { // simple test to see whether threads are enabled
                    var thread = await save.TextChannel.CreateThread(starterMessage.Id, x =>
                    {
                        x.Name = "Updates";
                        x.AutoArchiveDuration = 1440;
                    });
                    save.Thread = thread;
                }

                OnSave();
            }
            if (save == null)
                return;
            await save.TextChannel.AddPermissionOverwriteAsync(user, perms(manage));
            if (!manage)
            {
                await save.UpdateChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithTitle("User Joined Paired VC")
                    .WithDescription($"{user.GetName()} has joined the paired VC.\r\n" +
                    $"They have been granted permission to access this channel.")
                    .WithAuthor(user)
                    .Build());
            }
        }
        async Task UserLeftVc(SocketGuildUser user, SocketVoiceState state)
        {
            var voice = state.VoiceChannel;
            if(Pairings.TryGetValue(voice, out var save))
            {
                var pairings = voice.Users.Count(x => x.Id != user.Id && hasEnabledPairing(x, state.IsSelfMuted));
                var embed = new EmbedBuilder();
                embed.Title = $"User Left Paired VC";
                embed.Description = $"{user.GetName()} has left {voice.Name}, with {voice.Users.Count} remaining\r\n" +
                    $"Their permission to access this channel has been removed.";
                embed.WithAuthor(user);
                int hasEnabledPair = 0;
                var debug = new List<string>();
                foreach(var usr in voice.Users)
                {
                    if (usr.Id == user.Id)
                        continue;
                    var b = hasEnabledPairing(usr, usr.IsSelfMuted);
                    debug.Add($"{usr.GetName()}:{b:0}");
                    if (b)
                        hasEnabledPair++;
                }
                embed.WithFooter(Program.ToEncoded(string.Join(",", debug)));
                if (hasEnabledPair > 0)
                {
                    await save.TextChannel.RemovePermissionOverwriteAsync(user);
                    await save.UpdateChannel.SendMessageAsync(embed: embed.Build());
                    return;
                }
                await Program.AppInfo.Owner.SendMessageAsync(embed: embed.Build());
                await save.TextChannel.DeleteAsync();
                Pairings.TryRemove(voice, out _);
                OnSave();
            }
        }
        async Task UserMovedVc(SocketGuildUser user, SocketVoiceState fState, SocketVoiceState tState)
        {
            var from = fState.VoiceChannel;
            var to = tState.VoiceChannel;
            if(Pairings.TryGetValue(from, out var thing))
            {
                var pairings = from.Users.Count(x => x.Id != user.Id && hasEnabledPairing(x, fState.IsSelfMuted || tState.IsSelfMuted));
                if (pairings == 0)
                {
                    Pairings.TryRemove(from, out _);
                    if(thing.TextChannel != null)
                    { 
                        await thing.TextChannel.ModifyAsync(x =>
                        {
                            x.CategoryId = to.CategoryId;
                            x.Position = 999;
                            x.Topic = $"Channel paired to <#{to.Id}>";
                            x.Name = "pair-" + to.Name;
                        });
                        await thing.UpdateChannel.SendMessageAsync($"Channel is now paired to <#{to.Id}> as user moved.");
                        Pairings.TryRemove(from, out _);
                        Pairings[to] = thing;
                    }
                    OnSave();
                    return;
                }
            }
            await UserJoinedVc(user, tState);
        }

        async Task catchup()
        {
            var shouldSave = false;
            var toRemove = new List<SocketVoiceChannel>();
            foreach (var keypair in Pairings)
            {
                var voice = keypair.Key;
                var save = keypair.Value;
                if(save.TextChannel == null)
                {
                    toRemove.Add(voice);
                    continue;
                }
                bool persist = false;
                foreach(var usr in voice.Users)
                {
                    if (usr.IsBot) continue;
                    var bUser = Program.GetUser(usr);
                    if (bUser.Options.PairedVoiceChannels == CreateChannelForVoice.Never)
                        continue;
                    if (bUser.Options.PairedVoiceChannels == CreateChannelForVoice.WhenMuted && !usr.IsSelfMuted)
                        continue;
                    persist = true;
                }
                if(!persist)
                {
                    toRemove.Add(voice);
                    await save.TextChannel.DeleteAsync(new RequestOptions() { AuditLogReason = $"Paired to vc; no users to permit existance" });
                }
            }
            foreach (var vc in toRemove)
                Pairings.TryRemove(vc, out _);

            shouldSave = toRemove.Count > 0;
#if !DEBUG
            foreach(var g in Program.Client.Guilds)
            {
                var existing = await catchupVCs(g);
                shouldSave = shouldSave || existing;
            }
#endif
            if (shouldSave)
                OnSave();
        }

        async Task<bool> catchupVCs(SocketGuild guild)
        {
            var doneAny = false;
            foreach(var vc in guild.VoiceChannels)
            {
                if (Pairings.TryGetValue(vc, out _))
                    continue;
                var createdDueTo = vc.Users.FirstOrDefault(x => hasEnabledPairing(x, x.IsSelfMuted));
                if (createdDueTo == null)
                    continue;
                doneAny = true;
                await UserJoinedVc(createdDueTo, createdDueTo.VoiceState.Value);
                foreach (var other in vc.Users)
                    if (other.Id != createdDueTo.Id)
                        await UserJoinedVc(other, other.VoiceState.Value);
            }
            return doneAny;
        }

        public class syncSave
        {
            [JsonProperty("channel")]
            public ITextChannel TextChannel { get; set; }
            [JsonProperty("thread", NullValueHandling = NullValueHandling.Ignore)]
            public IThreadChannel Thread { get; set; }

            [JsonIgnore]
            public IMessageChannel UpdateChannel => (IMessageChannel)Thread ?? TextChannel;
        }
    }
}
