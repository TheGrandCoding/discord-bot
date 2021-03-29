using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using Discord;
using Discord.WebSocket;
using DiscordBot.Classes.Attributes;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services.Rules
{
    [RequireService(typeof(LoggingService))]
    public class PenaltyService : SavedService, ISARProvider
    {
        private static int _id;
        public Dictionary<int, Penalty> Penalties { get; set; } 
            = new Dictionary<int, Penalty>();
        public Dictionary<ulong, int> Escalations { get; set; } = new Dictionary<ulong, int>();
        public Semaphore Lock { get; } = new Semaphore(1, 1);

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
                var sv = ReadSave();
                var loaded = Program.Deserialise<PenaltySave>(sv);
                _id = loaded.PenaltyId;
                foreach (var pen in loaded.penalties)
                {
                    Penalties[pen.Id] = pen;
                }
            });
            new Thread(() =>
            {
                try
                {
                    doLoop(Program.GetToken());
                } catch(Exception ex)
                {
                    Program.LogMsg($"PenaltyThread", ex);
                    this.HasFailed = true;
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
        }

        async Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (arg3.Emote.Name != "⛔") return;
            if (!(arg2 is ITextChannel channel)) return;
            var message = await arg1.GetOrDownloadAsync();
            var user = await channel.GetUserAsync(arg3.UserId);
            await message.RemoveReactionAsync(arg3.Emote, user);
            if (user.GuildPermissions.Administrator == false)
            {
                await user.SendMessageAsync("You do not have permission to block content for this guild");
                return;
            }
            if(message.Attachments.Count == 0)
            {
                await user.SendMessageAsync("To block text content, you must use a topic block via command.");
                return;
            }
            var attachment = message.Attachments.First();
            var path = Path.Combine(Path.GetTempPath(), $"{message.Id}_{attachment.Filename}");
            if(File.Exists(path) == false)
            {
                using var wc = new WebClient();
                wc.DownloadFile(attachment.Url, path);
            }

            using var fstream = new FileStream(path, FileMode.Open);
            var algo = new DifferenceHash();
            var hash = algo.Hash(fstream);
            var p = await AddImageBlock(user as SocketGuildUser, null, "Via emoji, none given", null, hash, guildwide: true);
            await message.DeleteAsync(new RequestOptions()
            {
                AuditLogReason = "Adding image block"
            });
            await user.SendMessageAsync($"Added indefinite image block for hash {hash}; id of penalty: ${p.Id}");
        }

        public override string GenerateSave()
        {
            string s = "{}";
            execute(() =>
            {
                var ld = new PenaltySave()
                {
                    PenaltyId = _id,
                    penalties = Penalties.Values.ToList()
                };
                s = Program.Serialise(ld);
            });
            return s;
        }

        public Task<ITextChannel> GetAdminChannel(IGuild guild)
        {
            var srv = Program.Services.GetRequiredService<LoggingService>();
            return srv.GetChannel(guild, "penalty");
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

        #region Penalties
        public async Task AddMute(SocketGuildUser op, SocketGuildUser target, string reason, TimeSpan? duration)
        {
            await AddPenalty(new MutePenalty(op.Guild, op, target, reason, duration));
        }
        public async Task AddTempBan(SocketGuildUser op, SocketGuildUser target, string reason, TimeSpan? duration)
        {
            await AddPenalty(new TempBanPenalty(op.Guild, op, target, reason, duration));
        }
        public async Task AddTopicBlock(SocketGuildUser op, SocketGuildUser target, string reason, TimeSpan? duration, string regex, bool ignoreNsfw = false, bool guildwide = false)
        {
            await AddPenalty(new TopicBlockPenalty(op.Guild, op, target, reason, duration, regex)
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
    }

    public abstract class Penalty
    {
        public Penalty(SocketGuild guild, IGuildUser op, IGuildUser target, string reason, TimeSpan? duration)
        {
            Guild = guild;
            Operator = op;
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
                .WithColor(Color.Red);
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
            IRole mutedRole = Guild.Roles.FirstOrDefault(x => x.Name == "Muted");
            if(mutedRole == null)
            {
                mutedRole = await Guild.CreateRoleAsync("Muted", isMentionable: false);
            }
            return mutedRole;
        }

        public override async Task Set()
        {
            var mutedRole = await GetMutedRole();
            await Target.AddRoleAsync(mutedRole, new RequestOptions()
            {
                AuditLogReason = $"Muted for {Program.FormatTimeSpan(Duration.Value, true)}"
            });
            try
            {
                await Target.SendMessageAsync($"You have been muted in {Guild.Name} for {Program.FormatTimeSpan(Duration.Value)}");
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
            await message.DeleteAsync(new RequestOptions()
            {
                AuditLogReason = "User is muted, message is invalid - channel perms updated"
            });
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
                await message.DeleteAsync(new RequestOptions()
                {
                    AuditLogReason = $"User prohibited from discussing this topic | {Id}"
                });
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
            await message.DeleteAsync(new RequestOptions()
            {
                AuditLogReason = $"Image {sim:00}% similar to one blocked under {Id}"
            });
            await this.Escalate(message.Author as SocketGuildUser, $"Similarity: {sim:00.0}%");
        }

    }
}
