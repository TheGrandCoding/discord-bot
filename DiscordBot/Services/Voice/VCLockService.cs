using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class VCLockService : SavedService, ISARProvider
    {
        public Dictionary<ulong, VCLock> LockedChannels { get; set; }
        public override string GenerateSave()
        {
            return Program.Serialise(LockedChannels);
        }
        public override void OnReady()
        {
            Program.Client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
            var save = ReadSave();
            LockedChannels = Program.Deserialise<Dictionary<ulong, VCLock>>(save);
            var remove = new List<VCLock>();
            foreach (var x in LockedChannels.Values)
            {
                if(x.Voice.Users.Count == 0)
                {
                    remove.Add(x);
                } else
                {
                    x.SetLock();
                    bool kicked = false;
                    foreach(var u in x.Voice.Users)
                    {
                        if(!x.Authorised.Contains(u))
                        {
                            u.ModifyAsync(x =>
                            {
                                x.Channel = null;
                            });
                            kicked = true;
                        }
                    }
                    if (kicked)
                        x.Text.SendMessageAsync($"Removed users from {x.Name} who joined whilst bot offline");
                }
            }
            foreach (var x in remove)
                UnLockChannel(x);
        }

        async Task leftChannel(SocketUser user, SocketVoiceChannel chnl)
        {
            if (chnl == null)
                return;
            if(LockedChannels.TryGetValue(chnl.Id, out var vclock))
            {
                if(chnl.Users.Count == 0)
                {
                    UnLockChannel(vclock);
                    await vclock.Text.SendMessageAsync($"Automatically unlocked {vclock.Name} as everyone left");
                }
            }
        }

        async Task enterChannel(SocketGuildUser user, SocketVoiceChannel chnl, SocketVoiceChannel old)
        {
            if (chnl == null)
                return;
            if(LockedChannels.TryGetValue(chnl.Id, out var vc))
            {
                if(!vc.Authorised.Contains(user))
                {
                    await user.ModifyAsync(x =>
                    {
                        x.Channel = old;
                    });
                    await user.SendMessageAsync($"Voice channel {vc.Name} is locked;" +
                        $"\r\nOnly the following users may join it: " +
                        $"{string.Join(", ", vc.Authorised.Select(x => x.Mention))}");
                }
            }
        }

        private async Task Client_UserVoiceStateUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
        {
            if(arg3.VoiceChannel == null || arg3.VoiceChannel != arg2.VoiceChannel)
            {
                await leftChannel(arg1, arg2.VoiceChannel);
            }
            if (arg3.VoiceChannel != null && arg3.VoiceChannel != arg2.VoiceChannel)
            {
                await enterChannel(arg1 as SocketGuildUser, arg3.VoiceChannel, arg2.VoiceChannel);
            }
        }

        public VCLock LockChannel(SocketVoiceChannel voice, SocketTextChannel text)
        {
            var vclock = new VCLock()
            {
                Voice = voice,
                Text = text,
                UserLimit = voice.UserLimit,
                Name = voice.Name,
                Authorised = voice.Users.ToList(),
            };
            LockedChannels[voice.Id] = vclock;
            OnSave();
            return vclock;
        }

        public void UnLockChannel(VCLock lck)
        {
            LockedChannels.Remove(lck.Voice.Id);
            lck.Remove();
            OnSave();
        }

        public JToken GetSARDataFor(ulong userId)
        {
            if(LockedChannels.TryGetValue(userId, out var vc))
                return JToken.FromObject($"Storing your user id in reference to the locked voice channel {vc.Name}");
            return null;
        }
    }

    public class VCLock
    {
        public SocketVoiceChannel Voice { get; set; }
        public SocketTextChannel Text { get; set; }
        [JsonProperty("lim", NullValueHandling = NullValueHandling.Ignore)]
        public int? UserLimit { get; set; }
        public string Name { get; set; }
        public List<SocketGuildUser> Authorised { get; set; }

        public VCLock SetLock()
        {
            Voice.ModifyAsync(x =>
            {
                x.Name = $"🔒 {Name}";
                x.UserLimit = Authorised.Count;
            });
            return this;
        }

        public VCLock Remove()
        {
            Voice.ModifyAsync(x =>
            {
                x.Name = Name;
                x.UserLimit = UserLimit;
            });
            return this;
        }

        public VCLock With(SocketGuildUser user)
        {
            if(!Authorised.Contains(user))
                Authorised.Add(user);
            return this;
        }
    }
}
