using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Classes.DbContexts;
using DiscordBot.Utils;
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

    public class StatusLog
    {
        public long AuthorId { get; set; }

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

        public DateTime ChangedAt { get; set; }

        public string Status { get; set; }
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
        public DateTimeOffset Timestamp => Discord.SnowflakeUtils.FromSnowflake(Id);
        public DateTimeOffset CreatedAt => Discord.SnowflakeUtils.FromSnowflake(Id);
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
        public DbMsg(IServiceProvider provider, MsgModel model) : base(provider.GetRequiredService<MsgService>())
        {
            Id = model.Message;
            Content = service.GetLatestContent(Id, provider)?.Content.TrimEnd();
            Author = Program.Client.GetGuild(model.Guild)?.GetUser(model.Author) ?? null;
            Author ??= Program.Client.GetUser(model.Author);
            if(Author == null)
            {
                var dbu = new DbUser(model.Author);
                dbu.Username = service.GetNamesFor(model.Author, provider).LastOrDefault()?.Name;
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

        public string BannerId => throw new NotImplementedException();

        public Color? AccentColor => throw new NotImplementedException();

        IReadOnlyCollection<ClientType> IPresence.ActiveClients => throw new NotImplementedException();

        IReadOnlyCollection<IActivity> IPresence.Activities => throw new NotImplementedException();

        public Task<IDMChannel> CreateDMChannelAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public string GetAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
        {
            throw new NotImplementedException();
        }

        public string GetBannerUrl(ImageFormat format = ImageFormat.Auto, ushort size = 256)
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
        public override void OnReady(IServiceProvider services)
        {
#if !DEBUG
            Program.Client.PresenceUpdated += Client_PresenceUpdated;
            Program.Client.MessageReceived += Client_MessageReceived;
            Program.Client.MessageUpdated += Client_MessageUpdated;
            Program.Client.ChannelUpdated += Client_ChannelUpdated;
            Program.Client.GuildUpdated += Client_GuildUpdated;
            Catchup(services).Wait();
#endif

#if DEBUG
            //Catchup(services).Wait();
#endif
        }

        static Dictionary<ulong, DateTime> _presCache = new();
        private async Task Client_PresenceUpdated(SocketUser arg1, SocketPresence arg2, SocketPresence arg3)
        {
            if(arg2.Status != arg3.Status)
            {
                bool process = true;
                var now = DateTime.Now;
                if (_presCache.TryGetValue(arg1.Id, out var time))
                {
                    process = (now - time).TotalSeconds > 5;
                }
                _presCache[arg1.Id] = now;
                if(process)
                {
                    using var scope = Program.GlobalServices.CreateScope();
                    var db = scope.ServiceProvider.GetMsgDb("Presence");
                    var state = new StatusLog()
                    {
                        Author = arg1.Id,
                        ChangedAt = DateTime.UtcNow,
                        Status = arg3.Status.ToString()
                    };
                    await db.Status.AddAsync(state);
                    await db.SaveChangesAsync();
                }
            }
        }

        #region Startup catchup
        async Task Catchup(IServiceProvider services)
        {
            var _db_ = services.GetMsgDb("catchup");
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
                if (txt is IVoiceChannel) continue;
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
            await downloadLock.WaitAsync();
            try
            {
                var service = Program.GlobalServices.GetRequiredService<LoggingService>();
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

        SemaphoreSlim downloadLock = new(1);
        void downloadAttachmentThread(object arg)
        {
            if (!(arg is imageData data))
                return;
#if DEBUG
            return;
#endif
            try
            {
                using var scope = Program.GlobalServices.CreateScope();
                Info($"Getting lock on thread {Thread.CurrentThread.Name} | {Thread.CurrentThread.ManagedThreadId}");
                downloadLock.Wait();
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
                var service = Program.GlobalServices.GetRequiredService<LoggingService>();
                var chnl = service.GetChannel(data.Guild, "attachment").Result;
                Info($"Uploading {data.Attachment.Url}");
                var message = chnl.SendFileAsync(path, $"{data.Guild.Id}-{data.MessageId}").Result;
                Info($"Uploaded {data.Attachment.Url}");
                var _db_ = scope.ServiceProvider.GetMsgDb($"attachment-{data.MessageId}");
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

        async Task AddMessage(IMessage arg, LogContext _db_)
        {
            if (!(arg is IUserMessage umsg))
                return;
            if (!(arg.Channel is IGuildChannel guildChannel))
                return;
            if (arg.Author.IsBot || arg.Author.IsWebhook)
                return;
            try
            {
                await downloadLock.WaitAsync();
                var content = new MsgContent()
                {
                    Message = umsg.Id,
                    Content = umsg.Content,
                    Timestamp = umsg.Timestamp.DateTime,
                };
                _db_.Contents.Add(content);
                _db_.SaveChanges();
                var msg = new MsgModel(umsg);
                msg.ContentId = content.Id;
                _db_.Messages.Add(msg);
                await _db_.SaveChangesAsync();
            }
            finally
            {
                downloadLock.Release();
            }
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
            

        }

        private async Task Client_MessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        {
            if (arg2.Author.IsBot || arg2.Author.IsWebhook)
                return;
            using var scope = Program.GlobalServices.CreateScope();
            var _db_ = scope.ServiceProvider.GetMsgDb("messageUpdt");
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

        public List<MsgContent> GetContents(ulong message, IServiceProvider services)
        {
            var _db_ = services.GetMsgDb("getcontents");
            var t = _db_.Contents.AsQueryable().Where(x => x.MessageId == cast(message));
            return t.ToList();
        }
        public MsgContent GetLatestContent(ulong message, IServiceProvider service)
        {
            return GetContents(message, service).LastOrDefault();
        }

        public async Task<List<DbMsg>> GetMessagesAsync(ulong guild, ulong channel, IServiceProvider service, ulong before = ulong.MaxValue, int limit = 25)
        {
            var _db_ = service.GetMsgDb("getMsg");
            var query = _db_.Messages.AsQueryable().Where(x => x.GuildId == cast(guild) && x.ChannelId == cast(channel));
            var msgs = query.AsAsyncEnumerable()
                .Where(x => x.Message < before)
                .OrderByDescending(x => x.Message)
                .Take(limit);
            var result = await msgs.ToListAsync();
            return result.Select(x => new DbMsg(service, x)).ToList();
        }

        public async Task<DbMsg> GetMessageAsync(ulong messageId, IServiceProvider service)
        {
            var _db_ = service.GetMsgDb("getMsg");
            var model = await _db_.Messages.AsQueryable().FirstOrDefaultAsync(x => x.MessageId == cast(messageId));
            if (model == null)
                return null;
            return new DbMsg(service, model);
        }

        public async Task<List<ReturnedMsg>> GetCombinedMsgs(ulong guild, ulong channel, IServiceProvider service, ulong before = ulong.MaxValue, int limit = 25)
        {
            var _db_ = service.GetMsgDb("getCmbMsg");
            var fromDb = await GetMessagesAsync(guild, channel, service, before, limit);
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
            List<Task> parallel = new();
            foreach(var ds in dsMessages)
            {
                if (!(ds is IUserMessage umsg))
                    continue;
                if (!_db_.Messages.Any(x => x.MessageId == cast(ds.Id)))
                {
                    total.Add(new DiscordMsg(this, umsg));
#if !DEBUG
                    parallel.Add(Task.Run(async () => await AddMessage(umsg, _db_)));
#endif
                }
            }
            await Task.WhenAll(parallel.ToArray());
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
            using var scope = Program.GlobalServices.CreateScope();
            var _db_ = scope.ServiceProvider.GetMsgDb("gldUpdt");
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
            using var scope = Program.GlobalServices.CreateScope();
            var _db_ = scope.ServiceProvider.GetMsgDb("chnlUpdt");
            _db_.Names.Add(stamp);
            await _db_.SaveChangesAsync();
        }

        public List<NameTimestamps> GetNamesFor(ulong id, IServiceProvider service)
        {
            return service.GetMsgDb("getNames").Names.AsQueryable().Where(x => x.ObjectId == cast(id)).ToList();
        }
        public string GetNameForAndAt(ulong id, DateTime time, IServiceProvider service)
        {
            var stamps = GetNamesFor(id, service);
            foreach(var x in stamps)
            {
                if (x.Timestamp > time)
                    return x.Name;
            }
            return stamps.LastOrDefault()?.Name ?? "unknown name";
        }

        private async System.Threading.Tasks.Task Client_MessageReceived(SocketMessage arg)
        {
            await Task.Run(async () => {
                using var scope = Program.GlobalServices.CreateScope();
                await AddMessage(arg, scope.ServiceProvider.GetMsgDb("newMessage")); 
            });
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
