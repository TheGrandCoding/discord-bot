using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Classes.Attributes;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    [RequireService(typeof(MsgService))]
    [AlwaysSync]
    public class LoggingService : SavedService
    {
        public IGuild LogGuild { get; set; }

        public static Dictionary<ulong, string> MessagesDeletedByBot { get; } = new Dictionary<ulong, string>();

        Dictionary<ulong, guildSave> GuildMap { get; set; } = new Dictionary<ulong, guildSave>();
        Cached<bool> disconnected = new Cached<bool>(false, 15);

        Semaphore _auditLogLock = new Semaphore(1, 1);
        
        public async Task<Dictionary<string, int>> GetDeleter(IGuild guild, DbMsg message, DateTime deletedAt, Func<IUser, string> formatter)
        {
            var weightDictionary = new Dictionary<string, int>();
            if(MessagesDeletedByBot.TryGetValue(message.Id, out var botDelReason))
            {
                weightDictionary[$"mlapi: {botDelReason}"] = 666;
                return weightDictionary;
            }
            _auditLogLock.WaitOne();
            try
            {
                var log = await guild.GetAuditLogsAsync(limit: 15, actionType: ActionType.MessageDeleted);
                var correctChannel = log.Where(x =>
                {
                    var data = x.Data as MessageDeleteAuditLogData;
                    return data.ChannelId == message.ChannelId && data.Target.Id == message.Author.Id;
                });
                var ordered = correctChannel
                    .OrderBy(x => Math.Abs((x.CreatedAt - deletedAt).TotalMilliseconds));
                foreach(var thing in ordered)
                {
                    var usr = guild.GetUserAsync(thing.User.Id).Result;
                    var diff = DateTime.UtcNow - thing.CreatedAt.UtcDateTime;
                    var weight = 100 - (int)diff.TotalMinutes;
                    if(weight >= 0)
                        weightDictionary[formatter(usr)] = weight;
                }
                weightDictionary[formatter(message.Author)] = 2;
                weightDictionary["any bot"] = 1;
                weightDictionary["failed to fetch"] = disconnected.GetValueOrDefault(false) ? 25 : 0;
            } finally
            {
                _auditLogLock.Release();
            }
            return weightDictionary;
        }

        public override string GenerateSave()
        {
            var sv = new logSave()
            {
                guildId = LogGuild?.Id,
                guilds = GuildMap
            };
            return Program.Serialise(sv);
        }

        public override void OnReady()
        {
            var sv = ReadSave();
            var lg = Program.Deserialise<logSave>(sv);
            GuildMap = lg.guilds ?? new Dictionary<ulong, guildSave>();
            if(lg.guildId.HasValue)
            {
                LogGuild = Program.Client.GetGuild(lg.guildId.Value);
            } else
            {
                LogGuild = Program.Client.Guilds.FirstOrDefault(x => x.Name == "Logging Guild");
                if (LogGuild == null)
                {
                    var regions = Program.Client.GetOptimalVoiceRegionAsync().Result;
                    LogGuild = Program.Client.CreateGuildAsync("Logging Guild", regions).Result;
                    LogGuild.CreateRoleAsync("Log Master", new GuildPermissions(administrator: true), isMentionable:false).Wait();
                }
                OnSave();
            }
        }

        public override void OnLoaded()
        {
            Program.Client.MessageDeleted += Client_MessageDeleted;
            Program.Client.MessagesBulkDeleted += Client_MessagesBulkDeleted;
            Program.Client.MessageUpdated += Client_MessageUpdated;
            Program.Client.UserJoined += Client_UserJoined;
            Program.Client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
            Program.Client.ThreadCreated += Client_ThreadCreated;
            Program.Client.ThreadUpdated += Client_ThreadUpdated;
            Program.Client.ThreadDeleted += Client_ThreadDestroyed;
            Program.Client.Disconnected += async x =>
            {
                disconnected.Value = true;
            };
        }


        public override void OnDailyTick()
        {
            CheckPermsAll().Wait();
        }

        #region Permissions
        private async Task Client_UserJoined(SocketGuildUser arg)
        {
            if (arg.Guild.Id != LogGuild.Id)
                return;
            await CheckPermsFor(arg);
        }

        public async Task CheckPermsFor(IGuildUser user)
        {
            foreach(var key in GuildMap.Keys)
            {
                var guild = Program.Client.GetGuild(key);
                var save = GuildMap[key];
                var hasPerms = guild.GetUser(user.Id)?.GuildPermissions.Administrator ?? false;
                if (hasPerms)
                    await user.AddRoleAsync(save.Role);
                else
                    await user.RemoveRoleAsync(save.Role);
            }
        }
        public async Task CheckPermsAll()
        {
            foreach(var user in await LogGuild.GetUsersAsync())
            {
                if (user.IsBot)
                    continue;
                await CheckPermsFor(user);
            }
        }
        #endregion

        #region Fetching or creation channels

        public bool isDirty = false;

        static Semaphore Lock = new Semaphore(1, 1);

        async Task createCategory(IGuild guild, guildSave guildInfo)
        {
            guildInfo.Category = await LogGuild.CreateCategoryAsync(guild.Name);
            guildInfo.Role = await LogGuild.CreateRoleAsync(guild.Name, GuildPermissions.None, isMentionable:false);
            await guildInfo.Category.AddPermissionOverwriteAsync(LogGuild.EveryoneRole,
                new OverwritePermissions(viewChannel: PermValue.Deny, sendMessages: PermValue.Deny));
            await guildInfo.Category.AddPermissionOverwriteAsync(guildInfo.Role,
                new OverwritePermissions(viewChannel: PermValue.Allow));
        }

        public async Task<ICategoryChannel> GetCategory(IGuild guild)
        {
            if (GuildMap.TryGetValue(guild.Id, out var guildInfo))
            {
                if(guildInfo.Category == null)
                {
                    await createCategory(guild, guildInfo);
                    isDirty = true;
                } else if (guildInfo.Category.Name != guild.Name)
                {
                    await guildInfo.Category.ModifyAsync(x => x.Name = guild.Name);
                }
                return guildInfo.Category;
            }
            var map = new guildSave()
            {
                Actions = new Dictionary<string, ITextChannel>()
            };
            await createCategory(guild, map);
            GuildMap[guild.Id] = map;
            isDirty = true;
            return map.Category;
        }
        public async Task<ITextChannel> GetChannel(IGuild guild, string action)
        {
            Info($"Entering lock for {guild.Name}", action);
            Lock.WaitOne();
            Info($"Achieved lock for {guild.Name}", action);
            try
            {
                if(GuildMap.TryGetValue(guild.Id, out var guildSave))
                {
                    if (guildSave.Actions.TryGetValue(action, out var txt) && txt != null && !(txt is NullTextChannel))
                        return txt;
                }
                var category = await GetCategory(guild);
                var sv = GuildMap[guild.Id];
#if DEBUG
                var name = "dbg-" + action;
#else
                var name = "log-" + action;
#endif
                var newtxt = await LogGuild.CreateTextChannelAsync(name, x =>
                {
                    x.CategoryId = category.Id;
                    x.Topic = "Logs for any new, updated or deleted " + action + "s";
                });
                await newtxt.SyncPermissionsAsync();
                sv.Actions[action] = newtxt;
                isDirty = true;
                return newtxt;
            }
            finally
            {
                Lock.Release();
                Info($"Released lock for {guild.Name}", action);
            }
        }
#endregion

#region Helper Functions
        public async Task<IUserMessage> SendLog(IGuild guild, string action, EmbedBuilder builder, ulong? context = null)
        {
            if(!builder.Timestamp.HasValue)
                builder.WithCurrentTimestamp();
            if(context.HasValue)
            {
                var footer = builder.Footer ?? new EmbedFooterBuilder();
                footer.Text = (footer.Text == null ? "" : footer.Text + " ") + context.ToString();
                builder.Footer = footer;
            }
            var chnl = await GetChannel(guild, action);
            var msg = await chnl.SendMessageAsync(embed: builder.Build());
            return msg;
        }
        public async Task<IThreadChannel> GetThreadAsync(IGuild guild, string action, string name, IUserMessage message)
        {
            var chnl = await GetChannel(guild, action);
            return await chnl.CreateThreadAsync(name, 
                autoArchiveDuration: ThreadArchiveDuration.OneHour, 
                message: message);
        }
#endregion

#region Messages

        async Task<(EmbedBuilder, DbMsg)> getEmbedForDeletedMessage(Cacheable<IMessage, ulong> arg1, ITextChannel txt)
        {
            var service = Program.Services.GetRequiredService<MsgService>();
            var content = service.GetLatestContent(arg1.Id);
            var dbMsg = await service.GetMessageAsync(arg1.Id);
            var builder = new EmbedBuilder()
                .WithTitle("Message Deleted")
                .WithColor(Color.Red)
                .WithDescription(content?.Content ?? "[unknown last content]");
            builder.AddField("Channel", $"{txt.Mention}", true);
            if (dbMsg == null)
            {
                builder.AddField("No record", "Message could not be found in database.", true);
            } else 
            { 
                builder.AddField("Author", $"{dbMsg.Author.Id}\r\n<@{dbMsg.Author.Id}>", true);
            }
            builder.AddField("Original Sent", Discord.SnowflakeUtils.FromSnowflake(arg1.Id).ToString("dd/MM/yy HH:mm:ss.fff"), true);
            return (builder, dbMsg);
        }

        private async Task Client_MessageDeleted(Cacheable<IMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> cached)
        {
            var when = DateTime.Now;
            var arg2 = await cached.GetOrDownloadAsync();
            if (!(arg2 is ITextChannel txt))
                return;
            (EmbedBuilder builder, DbMsg dbMsg) = await getEmbedForDeletedMessage(arg1, txt);
            var message = await SendLog(txt.Guild, "messages", builder, arg1.Id);
            if (isDirty)
                OnSave();
            if (dbMsg == null)
                return;
            var data = new messageData()
            {
                deleted = dbMsg,
                guild = txt.Guild,
                builder = builder,
                log = message,
                when = when
            };
            new Thread(threadCheckDeleter).Start(data);
        }

        async Task bulkDeleteOtherThread(IReadOnlyCollection<Cacheable<IMessage, ulong>> arg1, ITextChannel channel)
        {
            var when = DateTimeOffset.Now;
            var desc = $"{arg1.Count} messages bulk deleted";
            string reason = null;
            var messages = arg1.OrderBy(x => x.Id);
            foreach (var x in messages)
            {
                if (reason == null)
                    reason = MessagesDeletedByBot.GetValueOrDefault(x.Id, null);
                var row = "\r\n" + x.Id.ToString();
                if (desc.Length + row.Length >= (EmbedBuilder.MaxDescriptionLength - 50))
                {
                    desc += "\r\n(+ more)";
                    break;
                }
                desc += row;
            }
            var starterMessage = await SendLog(channel.Guild, "bulkdelete", new EmbedBuilder()
                .WithTitle("Messages Bulk Deleted")
                .WithDescription(desc)
                .WithTimestamp(when)
                .AddField("Reason", reason ?? "[unknown]"));
            var thread = await GetThreadAsync(channel.Guild, "bulkdelete", $"Bulk delete in {channel.Name}", starterMessage);
            foreach (var x in messages)
            {
                (EmbedBuilder builder, DbMsg _) = await getEmbedForDeletedMessage(x, channel);
                await thread.SendMessageAsync(embed: builder
                    .WithFooter($"{x.Id}")
                    .Build());
            }
        }

        private async Task Client_MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> arg1, Cacheable<IMessageChannel, ulong> cached)
        {
            var arg2 = await cached.GetOrDownloadAsync();
            if (arg2 is ITextChannel channel)
            {
                var _ = Task.Run(async () =>
                {
                    try
                    {
                        await bulkDeleteOtherThread(arg1, channel);
                    }
                    catch (Exception ex)
                    {
                        Error(ex, "BulkDelete");
                    }
                });
            }
        }

        struct messageData
        {
            public DbMsg deleted;
            public IGuild guild;
            public EmbedBuilder builder;
            public IUserMessage log;
            public DateTime when;
        }

        void threadCheckDeleter(object o)
        {
            if (!(o is messageData data))
                return;
            Thread.Sleep(5000);
            var who = GetDeleter(data.guild, data.deleted, data.when, x => $"{x.Id} {x.Mention}").Result;
            var total = who.Values.Sum();
            string value = "";
            foreach(var keypair in who)
            {
                if (keypair.Value <= 0)
                    continue;
                var perc = keypair.Value / (double)total;
                value += keypair.Key + ": " + $"{perc}%\r\n";
            }
            var built = data.builder
                .AddField("Potential Causes", value, true)
                .Build();
            data.log.ModifyAsync(x => x.Embeds = new[] { built }).Wait();
        }

        private async Task Client_MessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        {
            if (!(arg3 is ITextChannel txt))
                return;
            if (arg2.Author.IsBot || arg2.Author.IsWebhook)
                return;
            var service = Program.Services.GetRequiredService<MsgService>();
            var contents = service.GetContents(arg1.Id);
            var latest = contents.OrderBy(x => x.Timestamp).Where(x => x.Content != arg2.Content).LastOrDefault();
            var latestContent = latest?.Content ?? null;
            if (latestContent == arg2.Content)
                return;
            var diff = DateTime.UtcNow - arg2.CreatedAt.UtcDateTime;
            if (latestContent == null && diff.TotalSeconds < 1)
                return;
            var builder = new EmbedBuilder()
                .WithTitle("Message Edited")
                .WithColor(Color.Blue)
                .WithDescription(latestContent ?? $"[unknown prior content {diff.TotalSeconds:0.0000}]");
            builder.AddField("Channel", txt.Mention, true);
            builder.AddField("Author", $"{arg2.Author.Id}\r\n<@{arg2.Author.Id}>", true);
            builder.AddField("Link", arg2.GetJumpUrl(), true);
            builder.AddField("Original Sent", Discord.SnowflakeUtils.FromSnowflake(arg2.Id).ToString("dd/MM/yy HH:mm:ss.fff"), true);
            await SendLog(txt.Guild, "messages", builder, arg1.Id);
            if (isDirty)
                OnSave();
        }

#endregion

#region Voice

        public Dictionary<ulong, DateTime> LastAction { get; set; } = new Dictionary<ulong, DateTime>();
        private async Task Client_UserVoiceStateUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
        {
            if (arg1.IsBot)
                return;
            var builder = new EmbedBuilder();
            builder.Title = $"VC Updated";
            builder.WithCurrentTimestamp();
            builder.AddField($"Time", DateTime.Now.ToString("HH:mm:ss.fff"), true);
            var last = LastAction.GetValueOrDefault(arg1.Id, DateTime.MinValue);
            if (last != DateTime.MinValue)
            {
                var diff = DateTime.Now - last;
                var d = Program.FormatTimeSpan(diff, true);
                if (string.IsNullOrWhiteSpace(d))
                    d = "0s";
                builder.AddField($"Duration", d, true);
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
            builder.AddField("Users", $"{afterUsers.Count}", true);
            await SendLog((arg2.VoiceChannel ?? arg3.VoiceChannel).Guild, "voice", builder, arg1.Id);
        }

        #endregion

        #region Threads
        private async Task Client_ThreadCreated(SocketThreadChannel arg)
        {
            var builder = new EmbedBuilder();
            builder.Title = "Thread Created";
            builder.AddField($"Name", arg.Name + "\r\n" + arg.Mention, true);
            builder.AddField("Created by", $"{(arg.Owner?.Mention ?? "n/a")}", true);
            builder.AddField("Channel", $"{(arg.ParentChannel?.Mention ?? "n/a")}", true);
            await SendLog(arg.Guild, "threads", builder, arg.Id);
        }

        private async Task Client_ThreadUpdated(SocketThreadChannel arg1, SocketThreadChannel arg2)
        {
            var calc = DiffCalculator.Create(arg1, arg2);
            var changes = calc.GetChanges();
            var builder = new EmbedBuilder();
            builder.Title = $"Thread Updated";
            builder.AddField($"Name", arg2.Name + "\r\n" + arg2.Mention, true);
            builder.AddField("Created by", $"{(arg2.Owner?.Mention ?? "n/a")}", true);
            builder.AddField("Channel", $"{(arg2.ParentChannel?.Mention ?? "n/a")}", true);

            foreach (var change in changes)
            {
                builder.AddField(change.Type, $"{change.Before} -> **{change.After}**", true);
            }
            await SendLog(arg1.Guild, "threads", builder, arg1.Id);
        }

        private async Task Client_ThreadDestroyed(Cacheable<SocketThreadChannel, ulong> cached)
        {
            var builder = new EmbedBuilder();
            builder.Title = "Thread Deleted";
            if(cached.HasValue)
            {
                var arg = await cached.GetOrDownloadAsync();
                builder.AddField($"Name", arg.Name + "\r\n" + arg.Mention, true);
                builder.AddField("Created by", $"{(arg.Owner?.Mention ?? "n/a")}", true);
                builder.AddField("Channel", $"{(arg.ParentChannel?.Mention ?? "n/a")}", true);
                await SendLog(arg.Guild, "threads", builder, arg.Id);
            } else
            {
                Program.LogWarning($"Unknown thread deleted: {cached.Id}", "ThreadDestroyed");
            }

        }

        #endregion



    }

    class logSave
    {
        public ulong? guildId;
        public Dictionary<ulong, guildSave> guilds;
    }

    class guildSave
    {
        [JsonProperty("c")]
        public ICategoryChannel Category { get; set; }
        [JsonProperty("a")]
        public Dictionary<string, ITextChannel> Actions { get; set; }
        [JsonProperty("r")]
        public IRole Role { get; set; }
    }
}
