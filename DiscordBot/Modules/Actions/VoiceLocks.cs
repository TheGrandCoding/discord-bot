using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes.Attributes;
using DiscordBot.Commands;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules.Actions
{
    [RequireService(typeof(VCLockService))]
    [Name("VC Locking")]
    public class VoiceLocks : BotModule
    {
        public VCLockService Locker { get; set; }

        [Command("lock"), Summary("Locks the voice channel you are in")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(ChannelPermission.ManageChannels)]
        public async Task<RuntimeResult> LockChannel()
        {
            if (!(Context.User is SocketGuildUser guser))
                return new BotResult($"Must be in a guild");
            if (guser.VoiceChannel == null)
                return new BotResult($"You are not in a voice channel");
            var vclock = Locker.LockChannel(guser.VoiceChannel, (SocketTextChannel)Context.Channel);
            vclock.SetLock();
            await ReplyAsync($"Voice channel has been locked only to its current members");
            return new BotResult();
        }

        [Command("unlock"), Summary("Unlocks the voice channel you are in")]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(ChannelPermission.ManageChannels)]
        public async Task<RuntimeResult> RescindLock()
        {
            if (!(Context.User is SocketGuildUser guser))
                return new BotResult($"Must be in a guild");
            if (guser.VoiceChannel == null)
                return new BotResult($"You are not in a voice channel");
            if(Locker.LockedChannels.TryGetValue(guser.VoiceChannel.Id, out var vclock))
            {
                if (vclock.Authorised.Contains(guser))
                {
                    Locker.UnLockChannel(vclock);
                    await ReplyAsync("Channel unlocked");
                } else
                {
                    return new BotResult($"Only those present when this was locked may unlock it");
                }
            } else
            {
                return new BotResult("The voice channel you are in is not locked");
            }
            return new BotResult();

        }
    }
}
