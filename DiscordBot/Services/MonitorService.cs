using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
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

        bool shouldAct(SocketGuildUser user)
        {
            foreach(var guild in Program.Client.Guilds)
            {
                var inG = guild.GetUser(user.Id);
                if (inG == null)
                    continue;
                if (guild.Id != user.Guild.Id)
                    return false;
                return true;
            }
            return true;
        }

        private async System.Threading.Tasks.Task Client_GuildMemberUpdated(SocketGuildUser arg1, SocketGuildUser arg2)
        {
            if (!shouldAct(arg1 ?? arg2))
                return;
            if (!Monitors.TryGetValue((arg1 ?? arg2).Id, out var monitor))
                return;
            var builder = new EmbedBuilder();
            builder.Title = $"User Updated";
            builder.WithCurrentTimestamp();
            builder.AddField($"Time", DateTime.Now.ToString("HH:mm:ss.fff"));
            builder.WithAuthor(arg1);
            var calc = new ChangeCalculator(arg1, arg2);
            var changes = calc.GetChanges();
            foreach(var change in changes)
            {
                builder.AddField(change.Type, $"{change.Before} -> {change.After}", true);
            }
            foreach (var usr in monitor.Status)
                await usr.SendMessageAsync(embed: builder.Build());
        }

        private async System.Threading.Tasks.Task Client_UserVoiceStateUpdated(Discord.WebSocket.SocketUser arg1, Discord.WebSocket.SocketVoiceState arg2, Discord.WebSocket.SocketVoiceState arg3)
        {
            if (!Monitors.TryGetValue(arg1.Id, out var monitor))
                return;
            var builder = new EmbedBuilder();
            builder.Title = $"VC Updated";
            builder.WithCurrentTimestamp();
            builder.AddField($"Time", DateTime.Now.ToString("HH:mm:ss.fff"));
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

    class ChangeCalculator
    {
        public SocketGuildUser Before { get; set; }
        public SocketGuildUser After { get; set; }
        public ChangeCalculator(SocketGuildUser before, SocketGuildUser after)
        {
            Before = before;
            After = after;
        }

        public List<Change> GetChanges()
        {
            var ls = new List<Change>();
            foreach(var x in this.GetType().GetMethods().Where(x => x.ReturnType == typeof(Change)))
            {
                ls.Add((Change)x.Invoke(this, new object[] { }));
            }
            return ls.Where(x => x != null).ToList();
        }

        public Change compareStatus()
        {
            return Before.Status == After.Status ? null : Change.Status(Before.Status, After.Status);
        }
        string getActivity(IActivity thing)
        {
            if (thing == null)
                return null;
            return $"{thing.Type} {thing.Name}";
        } 
        public Change compareActivity()
        {
            var before = Before.Activity;
            var after = After.Activity;
            if (after == null)
                return Change.Activity(getActivity(before), "[null]");
            if (before == null)
                return Change.Activity("[null]", getActivity(after));
            if (before.Type == ActivityType.Listening && after.Type == ActivityType.Listening)
                return null;
            if (before.Name == after.Name)
                return null;
            return Change.Activity(getActivity(before), getActivity(after));
        }
    }

    class Change
    {
        public string Type { get; set; }
        public string Before { get; set; }
        public string After { get; set; }
        public Change(string type, string before, string after)
        {
            Type = type;
            Before = before;
            After = after;
        }
        public static Change Status(UserStatus before, UserStatus after) 
            => new Change("Status", before.ToString(), after.ToString());
        public static Change Activity(string before, string after)
            => new Change("Activity", before, after);
    }

}
