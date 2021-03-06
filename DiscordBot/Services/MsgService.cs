﻿using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class LogContext : DbContext
    {
        public LogContext()
        {
            Program.LogInfo($"Creating LogContext", "Log-DB");
        }


        public DbSet<MsgModel> Messages { get; set; }
        public DbSet<NameTimestamps> Names { get; set; }
        public DbSet<MsgContent> Contents { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
#if WINDOWS
            options.UseSqlServer(Program.getDbString("BotLog"));
#else
                options.UseMySql(Program.getDbString("botData"),
                    new MariaDbServerVersion(new Version(10, 3, 25)), mysqlOptions =>
                    {
                        mysqlOptions.CharSet(CharSet.Utf8Mb4);
                    });
#endif
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MsgModel>()
                .HasKey(m => new { m.GuildId, m.ChannelId, m.MessageId });
            modelBuilder.Entity<NameTimestamps>()
                .HasKey(m => new { m.ObjectId, m.Timestamp });
            modelBuilder.Entity<MsgModel>()
                .Property(x => x.MessageId);
            modelBuilder.Entity<MsgContent>()
                .Property(x => x.Id)
                .UseIdentityColumn();
        }
    }

    public class MsgContent
    {
        public long Id { get; set; }
        public long MessageId { get; set; }

        [NotMapped]
        public ulong Message
        {
            get
            {
                unchecked
                {
                    return (ulong)MessageId;
                }
            }

            set
            {
                unchecked
                {
                    MessageId = (long)value;
                }
            }
        }

        public DateTime Timestamp { get; set; }
        public string Content { get; set; }
    }

    public class MsgModel
    {
        public long GuildId { get; set; }
        public long ChannelId { get; set; }
        public long MessageId { get; set; }
        public long AuthorId { get; set; }
        public long? ContentId { get; set; }
        public string Attachments { get; set; }

        [NotMapped]
        public ulong Guild
        {
            get
            {
                unchecked
                {
                    return (ulong)GuildId;
                }
            }

            set
            {
                unchecked
                {
                    GuildId = (long)value;
                }
            }
        }
        [NotMapped]
        public ulong Channel
        {
            get
            {
                unchecked
                {
                    return (ulong)ChannelId;
                }
            }

            set
            {
                unchecked
                {
                    ChannelId = (long)value;
                }
            }
        }
        [NotMapped]
        public ulong Message
        {
            get
            {
                unchecked
                {
                    return (ulong)MessageId;
                }
            }

            set
            {
                unchecked
                {
                    MessageId = (long)value;
                }
            }
        }
        [NotMapped]
        public ulong Author
        {
            get
            {
                unchecked
                {
                    return (ulong)AuthorId;
                }
            }

            set
            {
                unchecked
                {
                    AuthorId = (long)value;
                }
            }
        }

        public MsgModel(IUserMessage message)
        {
            Guild = ((IGuildChannel)message.Channel).GuildId;
            Channel = message.Channel.Id;
            Message = message.Id;
            Author = message.Author.Id;
            Attachments = string.Join(',', System.Linq.Enumerable.Select(message.Attachments, x => x.Url));
        }

        public MsgModel() { }
    }

    public abstract class ReturnedMsg
    {
        protected MsgService service;
        public ReturnedMsg(MsgService cn)
        {
            service = cn;
        }

        public ulong Id { get; set; }
        public string Content { get; set; }
        public DateTimeOffset Timestamp => SnowflakeUtils.FromSnowflake(Id);
        public DateTimeOffset CreatedAt => SnowflakeUtils.FromSnowflake(Id);
        public IUser Author { get; set; }
        public List<IEmbed> Embeds { get; set; }

        public string Attachments { get; set; }

        public bool IsDeleted { get; set; }
    }
    public class DiscordMsg : ReturnedMsg
    {
        public DiscordMsg(MsgService s, IUserMessage message) : base(s)
        {
            Id = message.Id;
            Content = message.Content;
            Author = message.Author;
            Attachments = string.Join(',', message.Attachments.Select(x => x.Url));
            Embeds = message.Embeds.ToList();
        }
    }
    public class DbMsg : ReturnedMsg
    {
        public DbMsg(MsgService s, MsgModel model) : base(s)
        {
            Id = model.Message;
            Content = s.GetLatestContent(Id)?.Content.TrimEnd();
            Author = Program.Client.GetGuild(model.Guild)?.GetUser(model.Author) ?? null;
            Author ??= Program.Client.GetUser(model.Author);
            if(Author == null)
            {
                var dbu = new DbUser(model.Author);
                dbu.Username = s.GetNamesFor(model.Author).LastOrDefault()?.Name;
                Author = dbu;
            }
            Attachments = model.Attachments;
            Embeds = new List<IEmbed>();
            ChannelId = model.Channel;
            Channel = Program.Client.GetChannel(ChannelId);
        }

        public SocketChannel Channel { get; }
        public ulong ChannelId { get; }
    }

    public class DbUser : IUser
    {
        public DbUser(ulong id)
        {
            Id = id;
        }
        public string AvatarId => throw new NotImplementedException();
        public string Discriminator { get; set; }
        public ushort DiscriminatorValue => throw new NotImplementedException();
        public bool IsBot => throw new NotImplementedException();
        public bool IsWebhook => throw new NotImplementedException();
        public string Username { get; set; }
        public DateTimeOffset CreatedAt => throw new NotImplementedException();
        public ulong Id { get; }
        public string Mention => throw new NotImplementedException();
        public IActivity Activity => throw new NotImplementedException();
        public UserStatus Status => throw new NotImplementedException();
        public UserProperties? PublicFlags => throw new NotImplementedException();
        public IImmutableSet<ClientType> ActiveClients => throw new NotImplementedException();

        public IImmutableList<IActivity> Activities => throw new NotImplementedException();

        public Task<IDMChannel> CreateDMChannelAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public string GetAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
        {
            throw new NotImplementedException();
        }
        public string GetDefaultAvatarUrl()
        {
            throw new NotImplementedException();
        }
        public Task<IDMChannel> GetOrCreateDMChannelAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }
    }

    public class NameTimestamps
    {
        public long ObjectId { get; set; }
        public DateTime Timestamp { get; set; }

        public string Name { get; set; }
    }

    public class MsgService : Service
    {
        public override bool IsCritical => true;
        public override int DefaultTimeout => base.DefaultTimeout * 100;
        public override void OnReady()
        {
#if !DEBUG
            Program.Client.MessageReceived += Client_MessageReceived;
            Program.Client.MessageUpdated += Client_MessageUpdated;
            Program.Client.ChannelUpdated += Client_ChannelUpdated;
            Program.Client.GuildUpdated += Client_GuildUpdated;
            Catchup().Wait();
#endif

#if DEBUG
            Catchup().Wait();
#endif
        }

        public static LogContext DB()
        {
            Program.LogInfo(Program.GetStackTrace(), $"Log-_db_");
            return Program.Services.GetRequiredService<LogContext>();
        }

        #region Startup catchup
        async Task Catchup()
        {
            using var _db_ = DB();
            foreach (var guild in Program.Client.Guilds)
            {
                if (guild.Id != 365230804734967840)
                    continue;
                try
                {
                    await Catchup(guild, _db_);
                } catch(Exception ex)
                {
                    Program.LogError(ex, "Cch-" + guild.Id.ToString());
                }
            }
            Info($"Done catchup, disposing of DB");
        }
        async Task Catchup(SocketGuild guild, LogContext _db_)
        {
            Debug($"Doing guild {guild.Name} - {guild.Id}", "Catchup");
            foreach(var txt in guild.TextChannels)
            {
                try
                {
                    await Catchup(txt, _db_);
                }
                catch (Exception ex)
                {
                    Program.LogError(ex, $"{guild.Id}-{txt.Id}");
                }
            }
        }
        async Task Catchup(SocketTextChannel txt, LogContext _db_)
        {
#if DEBUG
            int count = 0;
#endif
            Debug($"Doing #{txt.Name} - {txt.Id}", "Catchup");
            bool exit = false;
            ulong? before = null;
            int max = 25;
            do
            {
                IEnumerable<IMessage> msgs;
                if(before.HasValue)
                {
                    msgs = await txt.GetMessagesAsync(before.Value, Direction.Before, limit: max).FlattenAsync();
                }
                else
                {
                    msgs = await txt.GetMessagesAsync(limit: max).FlattenAsync();
                }
                var ordered = msgs.OrderByDescending(x => x.Id);
                // Highest first, meaning latest message
                foreach (var x in ordered)
                {
                    before = x.Id;
                    if (!(x is IUserMessage sm))
                        continue;
                    // x, starting at the latest message in the channel
                    if (_db_.Messages.Any(db => db.MessageId == cast(sm.Id)))
                    {
                        exit = true;
                        break;
                    }
                    await Task.Run(async () => await AddMessage(sm, _db_));
                }
                if(ordered.Count() < max)
                {
                    exit = true;
                    break;
                }
#if DEBUG
                if(count++ >= 1)
                {
                    exit = true;
                    break;
                }
#endif
            } while (!exit);
        }
        #endregion

        #region Image Handling

        struct imageData
        {
            public IAttachment Attachment;
            public IGuild Guild;
            public ulong MessageId;
        }

        void HandleAttachment(IAttachment attachment, IGuild guild, ulong id)
        {
            var data = new imageData()
            {
                Attachment = attachment,
                Guild = guild,
                MessageId = id
            };
#if THREADED
            var thr = new Thread(downloadAttachmentThread);
            thr.Name = $"dl-{attachment.Filename}";
            thr.Start(data);
#else
            downloadAttachmentThread(data);
#endif
        }

        public Dictionary<ulong, ulong> NewAttachmentMap { get; set; } = new Dictionary<ulong, ulong>();

        public async Task<IAttachment> GetSavedAttachment(IGuild guild, ulong originalId)
        {
            downloadLock.WaitOne();
            try
            {
                var service = Program.Services.GetRequiredService<LoggingService>();
                var chnl = service.GetChannel(guild, "attachment").Result;
                if (NewAttachmentMap.TryGetValue(originalId, out var newMsgId))
                {
                    var msg = await chnl.GetMessageAsync(newMsgId);
                    return msg?.Attachments.First() ?? null;
                }
                var messages = await chnl.GetMessagesAsync(originalId, Direction.After).FlattenAsync();
                foreach(var msg in messages)
                {
                    if (msg.Content.EndsWith(originalId.ToString()))
                        return msg?.Attachments.First() ?? null;
                }
                return null;
            }
            finally
            {
                downloadLock.Release();
            }
        }

        Semaphore downloadLock = new Semaphore(1, 1);
        void downloadAttachmentThread(object arg)
        {
            if (!(arg is imageData data))
                return;
#if DEBUG
            return;
#endif
            try
            {
                Info($"Getting lock on thread {Thread.CurrentThread.Name} | {Thread.CurrentThread.ManagedThreadId}");
                downloadLock.WaitOne();
                Info($"Got lock on thread {Thread.CurrentThread.Name} | {Thread.CurrentThread.ManagedThreadId}");
                Info($"Downloading {data.Attachment.Url}", "Attch");
                using var cl = new WebClient();
                cl.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                {
                    Info($"Progress {data.Attachment.Url}: {e.ProgressPercentage}%, {e.BytesReceived}");
                };
                var path = Path.Combine(Path.GetTempPath(), $"{data.MessageId}_{data.Attachment.Filename}");
                cl.DownloadFile(data.Attachment.Url, path);
                Info($"Downloaded {data.Attachment.Url}");
                var service = Program.Services.GetRequiredService<LoggingService>();
                var chnl = service.GetChannel(data.Guild, "attachment").Result;
                Info($"Uploading {data.Attachment.Url}");
                var message = chnl.SendFileAsync(path, $"{data.Guild.Id}-{data.MessageId}").Result;
                Info($"Uploaded {data.Attachment.Url}");
                using var _db_ = DB();
                MsgModel dbMsg;
                int tries = 0;
                do
                {
                    dbMsg = _db_.Messages.AsQueryable().FirstOrDefault(x => x.MessageId == cast(data.MessageId));
                    if (dbMsg == null)
                    {
                        if (tries > 20)
                        {
                            Error($"Cancel - Could not locate message {data.MessageId} for setting new attachment url.", "LogAtt");
                            return;
                        }
                        tries++;
                        Warning($"{tries:00} - Could not locate message {data.MessageId} for setting new attachment url.", "LogAtt");
                        Thread.Sleep(1000 * tries);
                    }
                } while (dbMsg == null);
                dbMsg.Attachments = dbMsg.Attachments.Replace(data.Attachment.Url, message.Attachments.First().Url);
                _db_.SaveChanges();
                NewAttachmentMap[data.MessageId] = message.Id;
                Warning($"Completed all {data.Attachment.Url}", "Attch");
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "DownloadAttch");
            }
            finally
            {
                Info($"Release lock on thread {Thread.CurrentThread.Name} | {Thread.CurrentThread.ManagedThreadId}");
                downloadLock.Release();
            }
        }

#endregion

        async Task AddMessage(IMessage arg, LogContext _db_ = null)
        {
            if (!(arg is IUserMessage umsg))
                return;
            if (!(arg.Channel is IGuildChannel guildChannel))
                return;
            if (arg.Author.IsBot || arg.Author.IsWebhook)
                return;
            var content = new MsgContent()
            {
                Message = umsg.Id,
                Content = umsg.Content,
                Timestamp = umsg.Timestamp.DateTime,
            };
            bool dispose = false;
            if(_db_ == null)
            {
                dispose = true;
                _db_ = DB();
            }
            _db_.Contents.Add(content);
            _db_.SaveChanges();
            var msg = new MsgModel(umsg);
            msg.ContentId = content.Id;
            _db_.Messages.Add(msg);
            await _db_.SaveChangesAsync();
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.White;
            Console.WriteLine($"{getWhere(umsg)}: {arg.Author.Username}: {arg.Content}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
            Info($"Starting attachment handle for {arg.Id} on thread {Thread.CurrentThread.Name} | {Thread.CurrentThread.ManagedThreadId}");
            foreach(var attch in umsg.Attachments)
            {
                HandleAttachment(attch, guildChannel.Guild, umsg.Id);
            }
            Info($"Ended attachment handle for {arg.Id} on thread {Thread.CurrentThread.Name} | {Thread.CurrentThread.ManagedThreadId}");
            if (dispose)
                _db_.Dispose();
        }

        private async Task Client_MessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        {
            if (arg2.Author.IsBot || arg2.Author.IsWebhook)
                return;
            using var _db_ = DB();
            var origMsg = await _db_.Messages.AsQueryable().FirstOrDefaultAsync(x => x.MessageId == cast(arg1.Id));
            if (origMsg == null)
                return; // TODO: add the message in
            var currentContent = _db_.Contents.First(x => x.Id == origMsg.ContentId);
            if (currentContent.Content == arg2.Content)
                return;
            var newContent = new MsgContent()
            {
                Content = arg2.Content,
                Timestamp = arg2.EditedTimestamp.GetValueOrDefault(DateTimeOffset.Now).DateTime,
                Message = arg2.Id
            };
            await _db_.Contents.AddAsync(newContent);
            await _db_.SaveChangesAsync();
            origMsg.ContentId = newContent.Id;
            await _db_.SaveChangesAsync();
        }

        bool isLessThan(string id1, ulong id2)
        {
            return ulong.Parse(id1) < id2;
        }

        public long cast(ulong t)
        {
            unchecked
            {
                return (long)t;
            }
        }

        public List<MsgContent> GetContents(ulong message)
        {
            using var _db_ = DB();
            var t = _db_.Contents.AsQueryable().Where(x => x.MessageId == cast(message));
            return t.ToList();
        }
        public MsgContent GetLatestContent(ulong message)
        {
            return GetContents(message).LastOrDefault();
        }

        public async Task<List<DbMsg>> GetMessagesAsync(ulong guild, ulong channel, ulong before = ulong.MaxValue, int limit = 25)
        {
            using var _db_ = DB();
            var query = _db_.Messages.AsQueryable().Where(x => x.GuildId == cast(guild) && x.ChannelId == cast(channel));
            var msgs = query.AsAsyncEnumerable()
                .Where(x => x.Message < before)
                .OrderByDescending(x => x.Message)
                .Take(limit);
            var result = await msgs.ToListAsync();
            return result.Select(x => new DbMsg(this, x)).ToList();
        }

        public async Task<DbMsg> GetMessageAsync(ulong messageId)
        {
            using var _db_ = DB();
            var model = _db_.Messages.AsQueryable().FirstOrDefault(x => x.MessageId == cast(messageId));
            if (model == null)
                return null;
            return new DbMsg(this, model);
        }

        public async Task<List<ReturnedMsg>> GetCombinedMsgs(ulong guild, ulong channel, ulong before = ulong.MaxValue, int limit = 25)
        {
            using var _db_ = DB();
            var fromDb = await GetMessagesAsync(guild, channel, before, limit);
            var total = new List<ReturnedMsg>();
            foreach (var x in fromDb)
                total.Add(x);
            var dsGuild = Program.Client.GetGuild(guild);
            if (dsGuild == null)
                return total;
            var dsChnl = dsGuild.GetTextChannel(channel);
            if (dsChnl == null)
                return total;
            IEnumerable<IMessage> dsMessages;
            if (before == ulong.MaxValue)
                dsMessages = await dsChnl.GetMessagesAsync(limit).FlattenAsync();
            else
                dsMessages = await dsChnl.GetMessagesAsync(before, Direction.Before, limit).FlattenAsync();
            bool changes = false;
            foreach(var x in total)
            {
                var inDs = dsMessages.FirstOrDefault(ds => ds.Id == x.Id);
                if (inDs == null)
                {
                    x.IsDeleted = true;
                    if (x.Content == null)
                        x.Content = "[Message content is not stored and cannot be fetched]";
                }
                else
                {
                    x.Embeds = inDs.Embeds.ToList();
                    if(x.Content == null)
                    {
                        x.Content = inDs.Content;
#if !DEBUG
                        var msgContent = new MsgContent()
                        {
                            Message = inDs.Id,
                            Timestamp = (inDs.EditedTimestamp ?? inDs.Timestamp).DateTime,
                            Content = inDs.Content
                        };
                        _db_.Contents.Add(msgContent);
                        changes = true;
#endif
                    }
                }
            }
            foreach(var ds in dsMessages)
            {
                if (!(ds is IUserMessage umsg))
                    continue;
                if (!_db_.Messages.Any(x => x.MessageId == cast(ds.Id)))
                {
                    total.Add(new DiscordMsg(this, umsg));
#if !DEBUG
                    await Task.Run(async () => await AddMessage(umsg));
#endif
                }
            }
            if (changes)
                await _db_.SaveChangesAsync();
            return total;
        }

        private async System.Threading.Tasks.Task Client_GuildUpdated(SocketGuild arg1, SocketGuild arg2)
        {
            if (arg1 == null || arg2 == null)
                return;
            if (arg1.Name == arg2.Name)
                return;
            var stamp = new NameTimestamps()
            {
                ObjectId = cast(arg2.Id),
                Timestamp = DateTime.Now,
                Name = arg2.Name
            };
            using var _db_ = DB();
            _db_.Names.Add(stamp);
            await _db_.SaveChangesAsync();
        }

        private async System.Threading.Tasks.Task Client_ChannelUpdated(SocketChannel arg1, SocketChannel arg2)
        {
            if (arg1 == null || arg2 == null)
                return;
            if (!(arg1 is SocketGuildChannel chnl1 && arg2 is SocketGuildChannel chnl2))
                return;
            if (chnl1.Name == chnl2.Name)
                return;
            var stamp = new NameTimestamps()
            {
                ObjectId = cast(chnl1.Id),
                Timestamp = DateTime.Now,
                Name = chnl2.Name
            };
            using var _db_ = DB();
            _db_.Names.Add(stamp);
            await _db_.SaveChangesAsync();
        }

        public List<NameTimestamps> GetNamesFor(ulong id)
        {
            using var _db_ = DB();
            return _db_.Names.AsQueryable().Where(x => x.ObjectId == cast(id)).ToList();
        }
        public string GetNameForAndAt(ulong id, DateTime time)
        {
            var stamps = GetNamesFor(id);
            foreach(var x in stamps)
            {
                if (x.Timestamp > time)
                    return x.Name;
            }
            return stamps.LastOrDefault()?.Name ?? "unknown name";
        }

        private async System.Threading.Tasks.Task Client_MessageReceived(SocketMessage arg)
        {
            await Task.Run(async () => await AddMessage(arg));
        }

        string getWhere(IUserMessage m)
        {
            var s = "{0}/{1}#{2}";
            var guildName = "";
            var categoryName = "";
            if (m.Channel is ITextChannel g)
            {
                guildName = g.Guild.Name;
                categoryName = g.GetCategoryAsync().Result?.Name ?? "";
            }
            return string.Format(s, guildName, categoryName, m.Channel.Name);
        }
    }
}
