using Discord;
using Discord.WebSocket;
using DiscordBot.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiscordBot.Services
{
    public class MonitorService : SavedService
    {
        public Dictionary<ulong, Monitor> Monitors { get; set; }
        public Dictionary<ulong, DateTime> LastAction { get; set; } = new Dictionary<ulong, DateTime>();

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
            builder.AddField($"Time", DateTime.Now.ToString("HH:mm:ss.fff"), true);
            builder.WithAuthor(arg1);
            builder.WithColor(Color.Purple);
            var calc = new ChangeCalculator(arg1, arg2);
            var changes = calc.GetChangesReflection();
            foreach(var change in changes)
            {
                string value = "";
                if (change.Before == null)
                    value = change.After;
                else
                    value = $"{change.Before} -> {change.After}";
                builder.AddField(change.Type, value, true);
            }
            foreach (var usr in monitor.Status)
                await usr.SendMessageAsync(embed: builder.Build());
        }

        private async System.Threading.Tasks.Task Client_UserVoiceStateUpdated(Discord.WebSocket.SocketUser arg1, Discord.WebSocket.SocketVoiceState arg2, Discord.WebSocket.SocketVoiceState arg3)
        {
            if (arg1.IsBot)
                return;
            var builder = new EmbedBuilder();
            builder.Title = $"VC Updated";
            builder.WithCurrentTimestamp();
            builder.AddField($"Time", DateTime.Now.ToString("HH:mm:ss.fff"));
            var last = LastAction.GetValueOrDefault(arg1.Id, DateTime.MinValue);
            if (last != DateTime.MinValue)
            {
                var diff = DateTime.Now - last;
                builder.AddField($"Duration", Program.FormatTimeSpan(diff, true), true);
            }
            LastAction[arg1.Id] = DateTime.Now;
            builder.WithAuthor($"{arg1.Username}#{arg1.Discriminator}", arg1.GetAnyAvatarUrl());
            var beforeUsers = arg2.VoiceChannel?.Users.ToList() ?? new List<SocketGuildUser>();
            var afterUsers = arg3.VoiceChannel?.Users.ToList() ?? new List<SocketGuildUser>();
            if (arg3.VoiceChannel == null)
            {
                builder.Description = $"{arg1.Username} left {arg2.VoiceChannel.Name}";
                beforeUsers.Add((SocketGuildUser)arg1);
                builder.WithColor(Color.Blue);
            }
            else if (arg2.VoiceChannel == null)
            {
                builder.Description = $"{arg1.Username} joined {arg3.VoiceChannel.Name}";
                builder.WithColor(Color.Green);
            }
            else if (arg2.VoiceChannel.Id != arg3.VoiceChannel.Id)
            {
                builder.Description = $"{arg1.Username} moved from {arg2.VoiceChannel.Name} to {arg3.VoiceChannel.Name}";
                builder.WithColor(Color.Teal);
            }
            if (string.IsNullOrWhiteSpace(builder.Description))
                return;
            var alreadyDoneMonitors = new List<ulong>();
            var alreadySent = new List<ulong>();
            foreach(var list in new List<List<SocketGuildUser>> { beforeUsers, afterUsers})
            {
                foreach(var usr in list)
                {
                    if (usr.IsBot)
                        continue;
                    if (alreadyDoneMonitors.Contains(usr.Id))
                        continue;
                    alreadyDoneMonitors.Add(usr.Id);
                    if(!Monitors.TryGetValue(usr.Id, out var monitor))
                        continue;
                    if (usr.Id != arg1.Id)
                        builder.WithFooter($"Proxy - {usr.Username}#{usr.Discriminator}", usr.GetAnyAvatarUrl());
                    foreach (var _u in monitor.VC)
                    {
                        if (alreadySent.Contains(_u.Id))
                            continue;
                        if (_u.Id == arg1.Id)
                            continue;
                        alreadySent.Add(_u.Id);
                        await _u.SendMessageAsync(embed: builder.Build());
                    }
                    break;
                }
            }
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

        /*public List<Change> GetChanges()
        {
            var ls = new List<Change>();
            foreach(var x in this.GetType().GetMethods().Where(x => x.ReturnType == typeof(Change)))
            {
                ls.Add((Change)x.Invoke(this, new object[] { }));
            }
            return ls.Where(x => x != null).ToList();
        }*/

        public List<Change> GetChangesReflection() 
        {
            var properties = typeof(SocketGuildUser).GetProperties();
            var ls = new List<Change>();
            foreach(var property in properties)
            {
                if (property.Name == "Guild")
                    continue;
                if (property.Name == "VoiceChannel"
                    || property.Name == "VoiceSessionId"
                    || property.Name == "VoiceState")
                    continue;
                if (property.Name == "MutualGuilds")
                    continue;
                if (IsPropertyACollection(property))
                    ls.AddRange(getChangesList(property));
                else
                    ls.AddRange(getChangesSingular(property));
            }
            return ls;
        }

        bool IsPropertyACollection(PropertyInfo property)
        {
            return property.PropertyType.GetInterface(typeof(IEnumerable<>).FullName) != null;
        }

        string toStr(IEnumerable list)
        {
            if (list == null)
                return "null";
            var sb = new StringBuilder();
            sb.Append("[");
            foreach (var x in list)
                sb.Append(toStr(x));
            sb.Append("]");
            return sb.ToString();
        }
        string toStr(object thing)
        {
            if (thing == null)
                return "null";
            if (thing is GuildPermissions perms)
            {
                return perms.RawValue.ToString();
            } else
            {
                return thing.ToString();
            }
        }

        List<Change> getChangesList(PropertyInfo property)
        {
            var before = ((IEnumerable)property.GetValue(Before)).Cast<object>();
            var after = ((IEnumerable)property.GetValue(After)).Cast<object>();
            if (before == null || after == null)
                return new Change(property.Name, toStr(before), toStr(after));
            var added = after.Where(x => before.Contains(x) == false).ToList();
            var removed = before.Where(x => after.Contains(x) == false).ToList();
            var ls = new List<Change>();
            if (added.Count > 0)
                ls.Add(new Change("+" + property.Name, null, toStr(added)));
            if (removed.Count > 0)
                ls.Add(new Change("-" + property.Name, null, toStr(removed)));
            return ls;
        }
        List<Change> getChangesSingular(PropertyInfo property)
        {
            var before = property.GetValue(Before);
            var after = property.GetValue(After);
            if (isEqual(before, after))
                return new List<Change>();
            return new Change(property.Name, toStr(before), toStr(after));
        }

        bool isEqual(object before, object after)
        {
            if (before == null && after == null)
                return true;
            if (before == null)
                return false;
            if (before is CustomStatusGame game && after is CustomStatusGame gameAfter)
                return (game.Emote?.Equals(gameAfter.Emote) ?? gameAfter.Emote == null) && game.State == gameAfter.State;
            return before.Equals(after);
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

        public static implicit operator List<Change>(Change This) => new List<Change>() { This };
    }

}
