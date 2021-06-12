using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBot.Classes.Attributes;
using DiscordBot.Services.BuiltIn;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services.Rules
{
    [RequireService(typeof(LoggingService), 
                    typeof(UserChangeService),
                    typeof(MessageComponentService))]
    public class PenaltyService : SavedService, ISARProvider
    {
        private static int _id;
        public Dictionary<int, Penalty> Penalties { get; set; } 
            = new Dictionary<int, Penalty>();
        public Dictionary<ulong, int> Escalations { get; set; } = new Dictionary<ulong, int>();
        public Dictionary<ulong, int> ChannelMuteEscalations { get; set; } = new Dictionary<ulong, int>();
        public Dictionary<ulong, string> DefaultDurations { get; set; } = new Dictionary<ulong, string>();
        public Semaphore Lock { get; } = new Semaphore(1, 1);
        public MessageComponentService MessageComponentService { get; private set; }


        private static readonly string[] Formats = {
            "%d'd'%h'h'%m'm'%s's'", //4d3h2m1s
            "%d'd'%h'h'%m'm'",      //4d3h2m
            "%d'd'%h'h'%s's'",      //4d3h  1s
            "%d'd'%h'h'",           //4d3h
            "%d'd'%m'm'%s's'",      //4d  2m1s
            "%d'd'%m'm'",           //4d  2m
            "%d'd'%s's'",           //4d    1s
            "%d'd'",                //4d
            "%h'h'%m'm'%s's'",      //  3h2m1s
            "%h'h'%m'm'",           //  3h2m
            "%h'h'%s's'",           //  3h  1s
            "%h'h'",                //  3h
            "%m'm'%s's'",           //    2m1s
            "%m'm'",                //    2m
            "%s's'",                //      1s
        };
        public static TimeSpan? GetDurationForDefault(string text)
        {
            if (TimeSpan.TryParseExact(text.ToLowerInvariant(), Formats, CultureInfo.InvariantCulture, out var ts))
                return ts;
            if (text.StartsWith("next:"))
            {
                var after = text.Substring("next:".Length);
                if (after.Length != 4)
                    return null;
                var hours = int.Parse(after.Substring(0, 2));
                var minutes = int.Parse(after.Substring(2, 2));
                var date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                    hours, minutes, 0);
                if (date < DateTime.Now)
                    date = date.AddDays(1);
                return date - DateTime.Now;
            }
            return null;
        }

        public void execute(Action act)
        {
            Lock.WaitOne();
            try
            {
                act();
            } finally
            {
                Lock.Release();
            }
        }

        void doLoop(CancellationToken token)
        {
            while(token.IsCancellationRequested == false)
            {
                bool changed = false;
                execute(() =>
                {
                    var rm = new List<int>();
                    foreach(var penalty in Penalties.Values)
                    {
                        if(penalty.Finished)
                        {
                            penalty.Unset();
                            rm.Add(penalty.Id);
                        }
                    }
                    foreach (var x in rm)
                        Penalties.Remove(x);
                    changed = rm.Count > 0;
                });
                if (changed)
                    OnSave();
                Thread.Sleep(5000);
            }
        }

        public override void OnReady()
        {
            execute(() =>
            {
                MessageComponentService = Program.Services.GetRequiredService<MessageComponentService>();
                var sv = ReadSave();
                var loaded = Program.Deserialise<PenaltySave>(sv);
                _id = loaded.PenaltyId;
                foreach (var pen in loaded.penalties)
                {
                    Penalties[pen.Id] = pen;
                }
                DefaultDurations = loaded.defaultDurations;
            });
            new Thread(() =>
            {
                try
                {
                    doLoop(Program.GetToken());
                } catch(Exception ex)
                {
                    Program.LogError(ex, $"PenaltyThread");
                    MarkFailed(ex);
                }
            }).Start();

            Program.Client.MessageReceived += async (SocketMessage message) =>
            {
                if (!(message is IUserMessage umsg) || !(umsg.Channel is ITextChannel chnl))
                    return;
                var ls = new List<Penalty>();
                execute(() =>
                {
                    foreach (var penalty in Penalties.Values)
                    {
                        if(penalty.Guild.Id == chnl.GuildId)
                        {
                            if(penalty.IsGuildPenalty || penalty.Target.Id == message.Author.Id)
                            {
                                ls.Add(penalty); // ensure invoke is thread safe
                            }
                        }
                    }
                });
                foreach (var x in ls)
                    await x.OnMessageReceived(umsg, chnl);
            };
            Program.Client.ReactionAdded += Client_ReactionAdded;
            var changes = Program.Services.GetRequiredService<UserChangeService>();
            changes.RolesAdded += User_RolesAdded;
            changes.RolesRemoved += User_RolesRemoved;
        }

        private async Task User_RolesRemoved(SocketGuildUser user, SocketRole[] removed)
        {
            var muted = await GetMutedRole(user.Guild);
            if (removed.Any(x => x.Id == muted.Id) == false)
                return;

            var penalty = FindPenalty(x =>
            {
                if (!(x is MutePenalty mute))
                    return false;
                return mute.Target?.Id == user.Id;
            });
            if(penalty != null)
            {
                RemovePenalty(penalty.Id);
                if(penalty.Duration.HasValue && penalty.Finished == false)
                {
                    var remaining = (penalty.Performed + penalty.Duration.Value) - DateTime.Now;
                    await user.Guild.PublicUpdatesChannel.SendMessageAsync(
                        $"Detected mute removed for {user.Mention} despite {Program.FormatTimeSpan(remaining)} remaining\r\n" +
                        $"The mute has been unregistered from the bot without issue.");
                }
                OnSave();
            }
        }

        EmbedBuilder getRoleMuteBuilder(SocketGuildUser user, MutePenalty penalty)
        {
            var builder = new EmbedBuilder();
            builder.Title = "Muted by Role";
            builder.Description = $"{user.Mention} has been muted through a role being added\r\n" +
                $"To set or change its duration, use the buttons below or the `{Program.Prefix}penalty duration {penalty.Id} [duration, eg 1h25m]` command";
            if (penalty.Duration.HasValue)
                builder.AddField("Duration", $"This mute will last for {Program.FormatTimeSpan(penalty.Duration.Value)}\r\n" +
                    $"It will be removed at {DateTime.Now.Add(penalty.Duration.Value):F}");
            else
                builder.AddField("Duration", "This mute is indefinite, and will last until the role is manually removed.");
            return builder;
        }

        private async Task User_RolesAdded(SocketGuildUser user, SocketRole[] added)
        {
            var muted = await GetMutedRole(user.Guild);
            if (added.Any(x => x.Id == muted.Id) == false)
                return;

            var penalty = FindPenalty(x =>
            {
                if (!(x is MutePenalty mute))
                    return false;
                if (mute.Guild.Id != user.Guild.Id)
                    return false;
                return x.Target?.Id == user.Id;
            }) as MutePenalty;
            if(penalty == null)
            {
                DefaultDurations.TryGetValue(user.Guild.Id, out var defStr);
                TimeSpan? duration = defStr == null ? null : GetDurationForDefault(defStr);
                penalty = await AddMute(user.Guild.CurrentUser, user, "Detected via role change", duration);

                var embed = getRoleMuteBuilder(user, penalty);
                var cBuilder = new ComponentBuilder();
                cBuilder.WithButton("Set duration to 1h", $"{penalty.Id}-0", ButtonStyle.Secondary);
                cBuilder.WithButton("+1 hour", $"{penalty.Id}-1", ButtonStyle.Success);
                cBuilder.WithButton("-1 hour", $"{penalty.Id}-2", ButtonStyle.Primary);

                cBuilder.WithButton("Remove Mute", $"{penalty.Id}-3", ButtonStyle.Danger, row: 1);

                // addmute already saves the service
                var msg = await user.Guild.PublicUpdatesChannel.SendMessageAsync(embed: embed.Build(),
                    component: cBuilder.Build());
                MessageComponentService.Register(msg, handleMuteChange, penalty.Id.ToString());
            }
        }

        public static async Task handleMuteChange(CallbackEventArgs e)
        {
            await e.Interaction.AcknowledgeAsync(InteractionResponseFlags.Ephemeral);
            var This = Program.Services.GetRequiredService<PenaltyService>();
            var array = e.ComponentId.Split('-').Select(x => int.Parse(x));
            var penaltyId = array.ElementAt(0);
            var actionType = array.ElementAt(1);
            var penalty = This.FindPenalty(x =>
            {
                return x.Id == penaltyId;
            }) as MutePenalty;
            if(penalty == null)
            {
                await e.Message.ModifyAsync(x =>
                {
                    x.Content = "*Mute has been removed*";
                    x.Components = new ComponentBuilder().Build();
                });
                await e.Interaction.FollowupAsync("This mute has already been removed and cannot be modified further",
                    ephemeral: true);
                return;
            }
            switch(actionType)
            {
                case 0:
                    penalty.Duration = new TimeSpan(1, 0, 0);
                    await e.Interaction.FollowupAsync($"{e.User.Mention} has set duration of mute for {penalty.Target.Mention} to one hour");
                    break;
                case 1:
                    penalty.Duration = penalty.Duration.GetValueOrDefault(TimeSpan.FromHours(0)).Add(TimeSpan.FromHours(1));
                    await e.Interaction.FollowupAsync($"{e.User.Mention} has increased duration of mute for {penalty.Target.Mention}" +
                        $" to {Program.FormatTimeSpan(penalty.Duration.Value)}");
                    break;
                case 2:
                    if(!penalty.Duration.HasValue || penalty.Duration.Value.TotalHours < 1)
                    {
                        await e.Interaction.FollowupAsync("Duration is already lower than one hour, cannot reduce by one.",
                            ephemeral: true);
                        return;
                    }
                    penalty.Duration = penalty.Duration.GetValueOrDefault(TimeSpan.FromHours(0)).Add(TimeSpan.FromHours(-1));
                    await e.Interaction.FollowupAsync($"{e.User.Mention} has decreased duration of mute for {penalty.Target.Mention}" +
                        $" to {Program.FormatTimeSpan(penalty.Duration.Value)}");
                    break;
                case 3:
                    This.RemovePenalty(penaltyId);
                    await e.Interaction.FollowupAsync($"{e.User.Mention} removed the mute of {penalty.Target.Mention}");
                    await e.Message.ModifyAsync(x =>
                    {
                        x.Content = "*This mute has been removed*";
                        x.Components = new ComponentBuilder().Build();
                    });
                    return;
                default:
                    await e.Interaction.FollowupAsync($"Unknown action: {actionType}.");
                    return;

            }
            await e.Message.ModifyAsync(x => x.Embed = This.getRoleMuteBuilder(penalty.Target as SocketGuildUser, penalty).Build());
            This.OnSave();
        }

        async Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> cached, SocketReaction arg3)
        {
            var arg2 = await cached.GetOrDownloadAsync();
            if (!(arg2 is ITextChannel channel)) return;
            if (arg3.Emote.Name == "⛔" || arg3.Emote.Name == "imageblock")
                await reactBlockImage(channel, arg1, arg3);
            else if (arg3.Emote.Name == "❌" || arg3.Emote.Name == "channelmute")
                await reactBlockChannel(channel, arg1, arg3);
        }

        async Task reactBlockChannel(ITextChannel channel, Cacheable<IUserMessage, ulong> cacheMsg, SocketReaction reaction)
        {
            var message = await cacheMsg.GetOrDownloadAsync();
            var user = await channel.GetUserAsync(reaction.UserId);
            await message.RemoveReactionAsync(reaction.Emote, user);
            if (user.GuildPermissions.Administrator == false)
            {
                await user.SendMessageAsync("You do not have permission to block content for this guild");
                return;
            }
            SocketGuildUser author = message.Author as SocketGuildUser;
            int count = ChannelMuteEscalations.GetValueOrDefault(author.Id, 0);
            int minutes = (int)Math.Pow(5, count);
            ChannelMuteEscalations[author.Id] = count + 1;
            var ts = TimeSpan.FromMinutes(minutes);
            var p = await AddChannelMute(channel, user as SocketGuildUser, author, "Via emoji, none given", ts);
            await message.DeleteAndTrackAsync($"Channel muted {p.Id}");
            await user.SendMessageAsync($"Remove message and denied user permission to talk in {channel.Mention}");
        }

        async Task reactBlockImage(ITextChannel channel, Cacheable<IUserMessage, ulong> cacheMessage, SocketReaction reaction)
        {
            var message = await cacheMessage.GetOrDownloadAsync();
            var user = await channel.GetUserAsync(reaction.UserId);
            await message.RemoveReactionAsync(reaction.Emote, user);
            if (user.GuildPermissions.Administrator == false)
            {
                await user.SendMessageAsync("You do not have permission to block content for this guild");
                return;
            }
            if (message.Attachments.Count == 0)
            {
                await user.SendMessageAsync("To block text content, you must use a topic block via command.");
                return;
            }
            var attachment = message.Attachments.First();
            var path = Path.Combine(Path.GetTempPath(), $"{message.Id}_{attachment.Filename}");
            if (File.Exists(path) == false)
            {
                using var wc = new WebClient();
                wc.DownloadFile(attachment.Url, path);
            }

            using var fstream = new FileStream(path, FileMode.Open);
            var algo = new DifferenceHash();
            var hash = algo.Hash(fstream);
            var p = await AddImageBlock(user as SocketGuildUser, null, "Via emoji, none given", null, hash, guildwide: true);
            await message.DeleteAndTrackAsync($"Violated image block {p.Id}");
            await user.SendMessageAsync($"Added indefinite image block for hash {hash}; id of penalty: {p.Id}");

        }

        public override string GenerateSave()
        {
            string s = "{}";
            execute(() =>
            {
                var ld = new PenaltySave()
                {
                    PenaltyId = _id,
                    penalties = Penalties.Values.ToList(),
                    defaultDurations = DefaultDurations
                };
                s = Program.Serialise(ld);
            });
            return s;
        }

        public Task<ITextChannel> GetAdminChannel(IGuild guild)
        {
            var srv = Program.Services.GetRequiredService<LoggingService>();
            var c = srv.GetChannel(guild, "penalty");
            srv.OnSave();
            return c;
        }

        public async Task<IRole> GetMutedRole(SocketGuild guild)
        {
            IRole mutedRole = guild.Roles.FirstOrDefault(x => x.Name == "Muted");
            if (mutedRole == null)
            {
                mutedRole = await guild.CreateRoleAsync("Muted", permissions: GuildPermissions.None, isMentionable: false);
                await guild.PublicUpdatesChannel.SendMessageAsync(
                    $"MLAPI has just created a muted role: {mutedRole.Mention}\r\n" +
                    "You may want to configure its permissions, though MLAPI will automatically delete messages and configure the channel as the muted user speaks");
            }
            return mutedRole;
        }

        public async Task<Penalty> AddPenalty(Penalty penalty)
        {
            var txt = await GetAdminChannel(penalty.Guild);
            Lock.WaitOne();
            try
            {
                penalty.Id = _id++;
                await penalty.Set();
                Penalties[penalty.Id] = penalty;
            } finally
            {
                Lock.Release();
            }
            OnSave();
            return penalty;
        }

        public void RemovePenalty(int id)
        {
            execute(() =>
            {
                if (Penalties.TryGetValue(id, out var penalty))
                {
                    penalty.Unset();
                }
                Penalties.Remove(id);
            });
        }

        public bool Modify(int id, Action<Penalty> action)
        {
            var found = false;
            execute(() =>
            {
                if (Penalties.TryGetValue(id, out var p))
                {
                    found = true;
                    action(p);
                }
            });
            return found;
        }

        public Penalty FindPenalty(Func<Penalty, bool> filter)
        {
            Lock.WaitOne();
            try
            {
                return Penalties.Values.FirstOrDefault(filter);
            } finally
            {
                Lock.Release();
            }
        }

        #region Penalties
        public async Task<MutePenalty> AddMute(SocketGuildUser op, SocketGuildUser target, string reason, TimeSpan? duration)
        {
            return await AddPenalty(new MutePenalty(op.Guild, op, target, reason, duration)) as MutePenalty;
        }
        public async Task<Penalty> AddTempBan(SocketGuildUser op, SocketGuildUser target, string reason, TimeSpan? duration)
        {
            return await AddPenalty(new TempBanPenalty(op.Guild, op, target, reason, duration));
        }
        public async Task<Penalty> AddTopicBlock(SocketGuildUser op, SocketGuildUser target, string reason, TimeSpan? duration, string regex, bool ignoreNsfw = false, bool guildwide = false)
        {
            return await AddPenalty(new TopicBlockPenalty(op.Guild, op, target, reason, duration, regex)
            {
                IgnoreNSFW = ignoreNsfw,
                IsGuildPenalty = guildwide
            });
        }
        public async Task<Penalty> AddImageBlock(SocketGuildUser op, SocketGuildUser target, string reason, TimeSpan? duration, ulong hash, bool ignoreNsfw = false, bool guildwide = false)
        {
            return await AddPenalty(new ImageBlockPenalty(op.Guild, op, target, reason, duration, hash)
            {
                IgnoreNSFW = ignoreNsfw,
                IsGuildPenalty = guildwide
            });
        }
        public async Task<ChannelMutePenalty> AddChannelMute(ITextChannel channel, SocketGuildUser op, SocketGuildUser target, string reason, TimeSpan? duration)
        {
            return (ChannelMutePenalty) await AddPenalty(new ChannelMutePenalty(channel, (op ?? target).Guild, op, target, reason, duration));
        }
        #endregion

        public JToken GetSARDataFor(ulong userId)
        {
            if(!Lock.WaitOne(30_000))
                return JObject.FromObject("*Failed to fetch data: could not achieve thread-lock*");
            try
            {
                var array = new JArray();
                foreach(var penalty in Penalties.Values)
                {
                    if(penalty.Operator?.Id == userId || penalty.Target?.Id == userId)
                    {
                        var obj = new JObject();
                        obj["id"] = penalty.Id;
                        obj["duration"] = penalty.Duration.ToString();
                        obj["performed"] = penalty.Performed.ToString();
                        if (penalty.Operator?.Id == userId)
                            obj["operator"] = userId;
                        if (penalty.Target?.Id == userId)
                            obj["target"] = userId;
                        obj["_type"] = penalty.GetType().Name;
                        array.Add(obj);
                    }
                }
                return array;

            } finally
            {
                Lock.Release();
            }
        }
    }

    class PenaltySave
    {
        public int PenaltyId { get; set; }
        [Newtonsoft.Json.JsonProperty(ItemTypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto)]
        public List<Penalty> penalties { get; set; } = new List<Penalty>();
        public Dictionary<ulong, string> defaultDurations { get; set; } = new Dictionary<ulong, string>();
    }

    public abstract class Penalty
    {
        public Penalty(SocketGuild guild, IGuildUser op, IGuildUser target, string reason, TimeSpan? duration)
        {
            Guild = guild;
            Operator = op ?? guild.CurrentUser;
            Target = target;
            IsGuildPenalty = Target == null;
            Reason = reason;
            Performed = DateTime.Now;
            Duration = duration;
            Service = Program.Services.GetRequiredService<PenaltyService>();
        }
        public int Id { get; set; }
        public SocketGuild Guild { get; set; }
        public IGuildUser Operator { get; set; }
        public IGuildUser Target { get; set; }
        public string Reason { get; set; }
        public DateTime Performed { get; set; }
        public TimeSpan? Duration { get; set; }
        [JsonIgnore]
        public bool Finished => Duration.HasValue ? DateTime.Now > Performed.Add(Duration.Value) : false;

        /// <summary>
        /// Whether the penalty applies to the whole guild
        /// </summary>
        public bool IsGuildPenalty { get; set; }
        
        protected PenaltyService Service { get; }

        public abstract Task Set();
        public abstract Task Unset();

        protected virtual EmbedBuilder GetBuilder(bool withOperator = true)
        {
            var em = new EmbedBuilder()
                .WithCurrentTimestamp()
                .WithDescription($"```\r\n{(Reason ?? "no reason")}\r\n```" +
                    (Duration.HasValue 
                        ? $"\r\nDuration: `{Program.FormatTimeSpan(Duration.Value, true)}`"
                        : "\r\nIndefinite"))
                .WithColor(Finished ? Color.Green : Color.Red);
            if (Target != null)
                em.WithAuthor(Target);
            if (withOperator)
                em.WithFooter($"{Operator.Username}#{Operator.Discriminator} | {Id}", Operator.GetAnyAvatarUrl());
            else
                em.WithFooter($"{Id}");
            return em;
        }

        #region Events
        public virtual Task OnMessageReceived(IUserMessage message, ITextChannel channel)
        {
            return Task.CompletedTask;
        }
        #endregion
    }

    public class MutePenalty : Penalty
    {
        public MutePenalty(SocketGuild guild, IGuildUser op, IGuildUser target, string reason, TimeSpan? duration) : base(guild, op, target, reason, duration)
        {
        }

        async Task<IRole> GetMutedRole()
        {
            return await Service.GetMutedRole(Guild);
        }

        public override async Task Set()
        {
            var mutedRole = await GetMutedRole();
            var user = Guild.GetUser(Target.Id);
            if(user.Roles.Any(x => x.Id == mutedRole.Id) == false)
            {
                await Target.AddRoleAsync(mutedRole, new RequestOptions()
                {
                    AuditLogReason = $"Muted for {Program.FormatTimeSpan(Duration.Value, true)}"
                });
            }
            try
            {
                var duration = Duration.HasValue ? "for " + Program.FormatTimeSpan(Duration.Value) : "until further notice";
                await Target.SendMessageAsync($"You have been muted in {Guild.Name} {duration}");
            } catch { }
            var txt = await Service.GetAdminChannel(Guild);
            await txt.SendMessageAsync(embed: GetBuilder()
                .WithTitle("User Muted")
                .Build());
        }

        public override async Task Unset()
        {
            var mutedRole = await GetMutedRole();
            await Target.RemoveRoleAsync(mutedRole);
            var txt = await Service.GetAdminChannel(Guild);
            await txt.SendMessageAsync(embed: GetBuilder()
                .WithTitle("Mute Expired")
                .WithColor(Color.Green)
                .Build());
        }

        public override async Task OnMessageReceived(IUserMessage message, ITextChannel channel)
        {
            if (channel.Name == "muted")
                return;
            var mutedRole = await GetMutedRole();
            await channel.AddPermissionOverwriteAsync(mutedRole, new OverwritePermissions(sendMessages: PermValue.Deny));
            await message.DeleteAndTrackAsync("User is muted");
        }
    }

    public class ChannelMutePenalty : Penalty
    {
        public ChannelMutePenalty(ITextChannel channel, SocketGuild guild, IGuildUser op, IGuildUser target, string reason, TimeSpan? duration) : base(guild, op, target, reason, duration)
        {
            Channel = channel;
        }

        public PermValue Prior = PermValue.Inherit;

        public ITextChannel Channel { get; set; }

        public async override Task Set()
        {
            var existing = Channel.GetPermissionOverwrite(Target);

            var overwrite = existing.GetValueOrDefault(new OverwritePermissions());
            Prior = overwrite.SendMessages;
            overwrite = overwrite.Modify(sendMessages: PermValue.Deny);
            await Channel.AddPermissionOverwriteAsync(Target, overwrite, new RequestOptions()
            {
                AuditLogReason = "User muted from this channel specifically"
            });

            var txt = await Service.GetAdminChannel(Guild);
            await txt.SendMessageAsync(embed: GetBuilder()
                .WithTitle("User Channel Muted")
                .Build());
        }

        public async override Task Unset()
        {
            var existing = Channel.GetPermissionOverwrite(Target);
            var overwrite = existing.GetValueOrDefault(new OverwritePermissions());
            overwrite = overwrite.Modify(sendMessages: Prior);
            if(overwrite.ToAllowList().Count == 0 && overwrite.ToDenyList().Count == 0)
            {
                await Channel.RemovePermissionOverwriteAsync(Target);
            } else
            {
                await Channel.AddPermissionOverwriteAsync(Target, overwrite, new RequestOptions()
                {
                    AuditLogReason = "Removed channel mute"
                });
            }
            var txt = await Service.GetAdminChannel(Guild);
            await txt.SendMessageAsync(embed: GetBuilder()
                .WithTitle("Removed Channel Mute")
                .Build());
        }
    }

    public class TempBanPenalty : Penalty
    {
        public TempBanPenalty(SocketGuild guild, IGuildUser op, IGuildUser target, string reason, TimeSpan? duration) : base(guild, op, target, reason, duration)
        {
        }

        public override async Task Set()
        {
            await Target.SendMessageAsync($"You have been banned from {Guild.Name}\r\nThis ban will be removed after {Program.FormatTimeSpan(Duration.Value)}");
            await Guild.AddBanAsync(Target, 0, $"By {Operator.Id}: {Reason}");
            var txt = await Service.GetAdminChannel(Guild);
            await txt.SendMessageAsync(embed: GetBuilder()
                .WithTitle("User Temp-banned")
                .Build());
        }

        public override async Task Unset()
        {
            try
            {
                await Guild.RemoveBanAsync(Target);
            }
            catch
            {
            }
            var txt = await Service.GetAdminChannel(Guild);
            await txt.SendMessageAsync(embed: GetBuilder()
                .WithTitle("User Unbanned")
                .WithColor(Color.Green)
                .Build());
        }
    }

    public abstract class ContentBlockPenalty : Penalty 
    {
        public ContentBlockPenalty(SocketGuild guild, IGuildUser op, IGuildUser target, string reason, TimeSpan? duration) : base(guild, op, target, reason, duration)
        {
        }

        /// <summary>
        /// Whether the content block ignores NSFW channels
        /// </summary>
        public bool IgnoreNSFW { get; set; }

        public virtual async Task Escalate(SocketGuildUser user, string reason = "")
        {
            reason = $"Auto-escalation for violation of {Id}\r\n" + reason;
            var current = Service.Escalations.GetValueOrDefault(user.Id, -1) + 1; // so first is 0
            Service.Escalations[user.Id] = current;
            if(current <= 3)
            { // Mute with increasing time
                var duration = TimeSpan.FromMinutes(Math.Pow(5, current)); // such that first is 5^0 = 1m
                await Service.AddMute(Guild.CurrentUser, user, reason, duration);
            } else
            {
                var adjusted = current - 4;
                // so that current = 4 -> adjusted = 0
                var duration = TimeSpan.FromMinutes(Math.Pow(5, adjusted)); // such that first is 5^0 = 1m
                await Service.AddTempBan(Guild.CurrentUser, user, $"Auto-escalation for violation of {Id}", duration);
            }
        }
    }


    public class TopicBlockPenalty : ContentBlockPenalty
    {
        public TopicBlockPenalty(SocketGuild guild, IGuildUser op, IGuildUser target, string reason, TimeSpan? duration, string regex) : base(guild, op, target, reason, duration)
        {
            RGX = regex;
        }

        public string RGX { get; set; }

        public override async Task Set()
        {
            var txt = await Service.GetAdminChannel(Guild);
            await txt.SendMessageAsync(embed: GetBuilder()
                .WithTitle("User Topic Blocked")
                .AddField($"Regex", $"`{RGX}`")
                .Build());
        }

        public override async Task Unset()
        {
            var txt = await Service.GetAdminChannel(Guild);
            await txt.SendMessageAsync(embed: GetBuilder()
                .WithTitle("Topic Block Expired")
                .WithColor(Color.Green)
                .Build());
        }

        public override async Task OnMessageReceived(IUserMessage message, ITextChannel channel)
        {
            if (IgnoreNSFW && channel.IsNsfw)
                return;
            var matches = Regex.Matches(message.Content, RGX);
            if(matches.Count > 0)
            {
                var txt = await Service.GetAdminChannel(Guild);
                var embed = GetBuilder(withOperator: false);
                embed.Description = $"Message {message.Id} removed and user escalated for discussing a blocked topic. Matched content below.";
                foreach(Match match in matches)
                {
                    if (embed.Fields.Count > 25)
                        break;
                    embed.AddField($"{match.Index}", Program.Clamp(match.Value, 256));
                }
                await txt.SendMessageAsync(embed: embed.Build());
                await message.DeleteAndTrackAsync($"Violation of topic block {Id}");
                await this.Escalate(message.Author as SocketGuildUser);
            }
        }
    }

    public class ImageBlockPenalty : ContentBlockPenalty
    {
        public ImageBlockPenalty(SocketGuild guild, IGuildUser op, IGuildUser target, string reason, TimeSpan? duration, ulong hash) : base(guild, op, target, reason, duration)
        {
            ImageHash = hash;
        }

        public ulong ImageHash { get; set; }

        public override async Task Set()
        {
            var txt = await Service.GetAdminChannel(Guild);
            await txt.SendMessageAsync(embed: GetBuilder()
                .WithTitle("Image Topic Blocked")
                .AddField($"Hash", $"`{ImageHash}`")
                .Build());
        }

        public override async Task Unset()
        {
            var txt = await Service.GetAdminChannel(Guild);
            await txt.SendMessageAsync(embed: GetBuilder()
                .WithTitle("Image Block Expired")
                .WithColor(Color.Green)
                .Build());
        }

        public override async Task OnMessageReceived(IUserMessage message, ITextChannel channel)
        {
            if (IgnoreNSFW && channel.IsNsfw)
                return;
            if (message.Attachments.Count == 0)
                return;
            var attachment = message.Attachments.First();
            var path = Path.Combine(Path.GetTempPath(), $"{message.Id}_{attachment.Filename}");
            if (File.Exists(path) == false)
            {
                using var wc = new WebClient();
                wc.DownloadFile(attachment.Url, path);
            }
            using var fstream = new FileStream(path, FileMode.Open);
            var algo = new DifferenceHash();
            var hash = algo.Hash(fstream);
            var sim = CompareHash.Similarity(ImageHash, hash);
            if (sim < 97.5)
                return;
            await message.DeleteAndTrackAsync($"Image {sim:00}% similar to one blocked under {Id}");
            await this.Escalate(message.Author as SocketGuildUser, $"Similarity: {sim:00.0}%");
        }

    }
}
