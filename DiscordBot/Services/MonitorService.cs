using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Services
{
    public class MonitorService : SavedService
    {
        public Dictionary<ulong, Monitor> Monitors { get; set; }
        public override string GenerateSave()
        {
            return Program.Serialise(Monitors);
        }
        public override void OnReady()
        {
            Monitors = Program.Deserialise<Dictionary<ulong, Monitor>>(ReadSave());
            Program.Client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
            Program.Client.GuildMemberUpdated += Client_GuildMemberUpdated;
        }

        private async System.Threading.Tasks.Task Client_GuildMemberUpdated(SocketGuildUser arg1, SocketGuildUser arg2)
        {
            if (!Monitors.TryGetValue((arg1 ?? arg2).Id, out var monitor))
                return;
            var builder = new EmbedBuilder();
            builder.Title = $"Status Updated";
            builder.WithAuthor(arg1);
            builder.Description = $"{arg1.Status} to {arg2.Status}";
            foreach (var usr in monitor.Status)
                await usr.SendMessageAsync(embed: builder.Build());
        }

        private async System.Threading.Tasks.Task Client_UserVoiceStateUpdated(Discord.WebSocket.SocketUser arg1, Discord.WebSocket.SocketVoiceState arg2, Discord.WebSocket.SocketVoiceState arg3)
        {
            if (!Monitors.TryGetValue(arg1.Id, out var monitor))
                return;
            var builder = new EmbedBuilder();
            builder.Title = $"VC Updated";
            builder.WithAuthor(arg1);
            if (arg3.VoiceChannel == null)
                builder.Description = $"Left {arg2.VoiceChannel.Name}";
            else if (arg2.VoiceChannel == null)
                builder.Description = $"Join {arg2.VoiceChannel.Name}";
            else
                builder.Description = $"Moved from {arg2.VoiceChannel.Name} to {arg3.VoiceChannel.Name}";
            foreach (var usr in monitor.VC)
                await usr.SendMessageAsync(embed: builder.Build());
        }
    }

    public class Monitor
    {
        public List<SocketGuildUser> VC { get; set; } = new List<SocketGuildUser>();
        public List<SocketGuildUser> Status { get; set; } = new List<SocketGuildUser>();
    }

}
