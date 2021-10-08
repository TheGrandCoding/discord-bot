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
        public ConcurrentDictionary<SocketVoiceChannel, IThreadChannel> Threads { get; set; } = new ConcurrentDictionary<SocketVoiceChannel, IThreadChannel>();
        public ConcurrentDictionary<SocketVoiceChannel, ITextChannel> PairedChannels { get; set; } = new ConcurrentDictionary<SocketVoiceChannel, ITextChannel>();

        public override string GenerateSave()
        {
            var save = new syncSave()
            {
                threads = new Dictionary<ulong, IThreadChannel>(Threads.Select(x => new KeyValuePair<ulong, IThreadChannel>(x.Key.Id, x.Value))),
                texts = new Dictionary<ulong, ITextChannel>(PairedChannels.Select(x => new KeyValuePair<ulong, ITextChannel>(x.Key.Id, x.Value)))
            };
            return Program.Serialise(save);
        }

        public override void OnLoaded()
        {
            var sv = Program.Deserialise<syncSave>(ReadSave());
            Threads = new ConcurrentDictionary<SocketVoiceChannel, IThreadChannel>();
            foreach (var keypair in (sv.threads ?? new Dictionary<ulong, IThreadChannel>()))
                Threads[Program.Client.GetChannel(keypair.Key) as SocketVoiceChannel] = keypair.Value;
            PairedChannels = new ConcurrentDictionary<SocketVoiceChannel, ITextChannel>();
            foreach (var keypair in (sv.texts ?? new Dictionary<ulong, ITextChannel>()))
                PairedChannels[Program.Client.GetChannel(keypair.Key) as SocketVoiceChannel] = keypair.Value;
            catchup().Wait();
            Program.Client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
            Program.Client.ThreadUpdated += Client_ThreadUpdated;
        }

        private async Task Client_ThreadUpdated(Cacheable<SocketThreadChannel, ulong> cached1, SocketThreadChannel arg2)
        {
            var arg1 = await cached1.GetOrDownloadAsync();
            if (arg1 == null || arg2 == null)
                return;
            if(arg1.Archived == false && arg2.Archived == true)
            {
                // thread has been archived, let's see whether it was one of ours
                var find = Threads.FirstOrDefault(x => x.Value.Id == arg2.Id);
                var vc = find.Key;
                var thread = find.Value;
                if (vc == null || thread == null)
                    return;

                int hasEnabledPair = 0;
                foreach (var usr in vc.Users)
                {
                    var b = hasEnabledPairing(usr, usr.IsSelfMuted);
                    if (b)
                        hasEnabledPair++;
                }
                if(hasEnabledPair > 0)
                {
                    await thread.ModifyAsync(x =>
                    {
                        x.Archived = false;
                    });
                }
            }
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

        const string PRIVATE_THREADS = "PRIVATE_THREADS";
        async Task UserJoinedVc(SocketGuildUser user, SocketVoiceState state)
        {
            var voice = state.VoiceChannel;
            bool manage = false;
            if(!PairedChannels.TryGetValue(voice, out var pairedChannel))
            {
                var txts = user.Guild.TextChannels.Where(x => x.CategoryId == voice.CategoryId.GetValueOrDefault(0));
                pairedChannel = txts.FirstOrDefault();
            }
            if (pairedChannel == null)
                return;
            if(Threads.TryGetValue(voice, out var thread))
            {
            } else if(hasEnabledPairing(user, state.IsSelfMuted))
            {
                manage = true;

                var embed = new EmbedBuilder()
                        .WithTitle("Paired Channel")
                        .WithDescription("This thread can be used for discussions in the paired voice channel.")
                        .WithAuthor(user);
                var name = $"Paired with {voice.Name}";
                thread = voice.Guild.ThreadChannels.FirstOrDefault(x => x.Name == name);
                if(thread == null)
                {
                    if (voice.Guild.Features.Contains(PRIVATE_THREADS))
                    {
                        thread = await pairedChannel.CreateThreadAsync(name, type: ThreadType.PrivateThread, autoArchiveDuration: ThreadArchiveDuration.OneDay);
                        await thread.SendMessageAsync(embed: embed.Build());
                    } else
                    {
                        var starterMessage = await pairedChannel.SendMessageAsync(embed: embed.Build());
                        thread = await pairedChannel.CreateThreadAsync(name, autoArchiveDuration: ThreadArchiveDuration.OneDay, message: starterMessage);
                    }
                } else
                {
                    embed.Description = "This thread has been re-paired with the voice channel.";
                    await thread.SendMessageAsync(embed: embed.Build());
                }
                Threads[voice] = thread;
                foreach(var usr in voice.Users)
                {
                    await thread.AddUserAsync(usr);
                }

                OnSave();
            }
            if(thread == null)
            {
                Warning($"Thread is null when handling {user.Username} joining {pairedChannel.Name}. Weird?");
                return;
            }
            await thread.AddUserAsync(user, null);
            if (thread.AutoArchiveDuration == ThreadArchiveDuration.OneHour)
                await thread.ModifyAsync(x => x.AutoArchiveDuration = ThreadArchiveDuration.OneDay);
            if (!manage)
            {
                await thread.SendMessageAsync(embed: new EmbedBuilder()
                    .WithTitle("User Joined Paired VC")
                    .WithDescription($"{user.GetName()} has joined the paired VC.\r\n" +
                    $"They have been added to this thread.")
                    .WithAuthor(user)
                    .Build());
            }
        }
        async Task UserLeftVc(SocketGuildUser user, SocketVoiceState state)
        {
            var voice = state.VoiceChannel;
            if(Threads.TryGetValue(voice, out var thread))
            {
                var pairings = voice.Users.Count(x => x.Id != user.Id && hasEnabledPairing(x, state.IsSelfMuted));
                var embed = new EmbedBuilder();
                embed.Title = $"User Left Paired VC";
                embed.Description = $"{user.GetName()} has left {voice.Name}, with {voice.Users.Count} remaining\r\n" +
                    $"They have been removed from this thread.";
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
                await thread.RemoveUserAsync(user, null);
                await thread.SendMessageAsync(embed: embed.Build());
                if(hasEnabledPair == 0)
                {
                    await thread.ModifyAsync(x =>
                    {
                        x.AutoArchiveDuration = ThreadArchiveDuration.OneHour;
                    });
                }
                Threads.TryRemove(voice, out _);
                OnSave();
            }
        }
        async Task UserMovedVc(SocketGuildUser user, SocketVoiceState fState, SocketVoiceState tState)
        {
            var from = fState.VoiceChannel;
            var to = tState.VoiceChannel;
            if(Threads.TryGetValue(from, out var thread))
            {
                var pairings = from.Users.Count(x => x.Id != user.Id && hasEnabledPairing(x, fState.IsSelfMuted || tState.IsSelfMuted));
                if (pairings == 0)
                {
                    Threads.TryRemove(from, out _);
                    await thread.ModifyAsync(x =>
                    {
                        x.Name = $"Paired with {to.Name}";
                    });
                    await thread.SendMessageAsync($"Channel is now paired to <#{to.Id}> as the last user moved.");
                    Threads[to] = thread;
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
            foreach (var keypair in Threads)
            {
                var voice = keypair.Key;
                var thread = keypair.Value;
                if(thread == null)
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
                    if(thread.Archived == false)
                    {
                        await thread.ModifyAsync(x =>
                        {
                            x.Archived = true;
                            x.Locked = false;
                        }, new RequestOptions() { AuditLogReason = $"Paired to vc; no users to permit existance" });
                    }
                }
            }
            foreach (var vc in toRemove)
                Threads.TryRemove(vc, out _);

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
                if (Threads.TryGetValue(vc, out _))
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
            public Dictionary<ulong, IThreadChannel> threads;
            public Dictionary<ulong, ITextChannel> texts;
        }
    }
}
