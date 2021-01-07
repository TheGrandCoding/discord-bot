using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Classes.Attributes;
using DiscordBot.Services.Games;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class VCTextService : SavedService
    {
        public Dictionary<SocketVoiceChannel, ulong> Pairings { get; set; }

        public override string GenerateSave()
        {
            var dict = new Dictionary<string, ulong>();
            var conv = new DiscordConverter();
            foreach (var keypair in Pairings)
                dict[conv.GetValue(keypair.Key)] = keypair.Value;
            return Program.Serialise(dict);
        }

        public override void OnLoaded()
        {
            var sv = Program.Deserialise<Dictionary<string, ulong>>(ReadSave());
            Pairings = new Dictionary<SocketVoiceChannel, ulong>();
            foreach(var keypair in sv)
            {
                var split = keypair.Key.Split('.');
                var guild = Program.Client.GetGuild(ulong.Parse(split[0]));
                var voice = guild.GetVoiceChannel(ulong.Parse(split[1]));
                Pairings[voice] = keypair.Value;
            }
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
            bool shouldHaveChnl = hasEnabledPairing(user, arg2.IsSelfMuted || arg3.IsSelfMuted);
            if (arg3.VoiceChannel == null)
                await UserLeftVc(user, arg2);
            else if (arg2.VoiceChannel == null)
                await UserJoinedVc(user, arg3);
            else if (arg2.VoiceChannel.Id != arg3.VoiceChannel.Id)
                await UserMovedVc(user, arg2, arg3);
        }

        async Task JoinSyncPerms()
        {

        }

        OverwritePermissions perms(bool manage)
        {
            return new OverwritePermissions(
                viewChannel: PermValue.Allow,
                manageChannel: manage ? PermValue.Allow : PermValue.Inherit
                );
        }

        async Task UserJoinedVc(SocketGuildUser user, SocketVoiceState state)
        {
            var voice = state.VoiceChannel;
            ITextChannel text = null;
            bool manage = false;
            if(Pairings.TryGetValue(voice, out var txtId))
            {
                text = user.Guild.GetTextChannel(txtId);
            } else if(hasEnabledPairing(user, state.IsSelfMuted))
            {
                manage = true;
                var ls = new List<Overwrite>();
                ls.Add(new Overwrite(user.Guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)));
                foreach(var x in voice.Users)
                {
                    ls.Add(new Overwrite(x.Id, PermissionTarget.User, perms(x.Id == user.Id)));
                }
                text = await user.Guild.CreateTextChannelAsync("pair-" + voice.Name, x =>
                {
                    x.PermissionOverwrites = ls;
                    x.CategoryId = voice.CategoryId;
                    x.Position = 999;
                    x.Topic = $"Paired channel with <#{voice.Id}>.";
                });
                Pairings.Add(voice, text.Id);
                OnSave();
                await text.SendMessageAsync(embed: new EmbedBuilder()
                    .WithTitle("Paired Channel")
                    .WithDescription("This channel will be deleted once the user leaves the paired voice channel.")
                    .WithAuthor(user)
                    .Build());
            }
            if (text == null)
                return;
            await text.AddPermissionOverwriteAsync(user, perms(manage));
        }
        async Task UserLeftVc(SocketGuildUser user, SocketVoiceState state)
        {
            var voice = state.VoiceChannel;
            if(Pairings.TryGetValue(voice, out var txtId))
            {
                var pairings = voice.Users.Count(x => x.Id != user.Id && hasEnabledPairing(x, state.IsSelfMuted));
                var text = voice.Guild.GetTextChannel(txtId);
                if (pairings > 0)
                {
                    await text.RemovePermissionOverwriteAsync(user);
                    return;
                }
                await text.DeleteAsync();
                Pairings.Remove(voice);
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
                    Pairings.Remove(from);
                    var text = from.Guild.GetTextChannel(thing);
                    if(text != null)
                    { 
                        await text.ModifyAsync(x =>
                        {
                            x.CategoryId = to.CategoryId;
                            x.Position = 999;
                            x.Topic = $"Channel paired to <#{to.Id}>";
                            x.Name = "pair-" + to.Name;
                        });
                        await text.SendMessageAsync($"Channel is now paired to <#{to.Id}> as user moved.");
                        Pairings.Remove(from);
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
            var toRemove = new List<SocketVoiceChannel>();
            foreach(var keypair in Pairings)
            {
                var voice = keypair.Key;
                var txt = voice.Guild.GetTextChannel(keypair.Value);
                if(txt == null)
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
                    await txt.DeleteAsync(new RequestOptions() { AuditLogReason = $"Paired to vc; no users to permit existance" });
                }
            }
            foreach (var vc in toRemove)
                Pairings.Remove(vc);
            if (toRemove.Count > 0)
                OnSave();
        }
    }
}
