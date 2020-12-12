using Discord;
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
        }
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
        public IImmutableSet<ClientType> ActiveClients => throw new NotImplementedException();

        public IImmutableList<IActivity> Activities => throw new NotImplementedException();

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
        public ulong ObjectId { get; set; }
        public DateTime Timestamp { get; set; }

        public string Name { get; set; }
    }

    public class MsgService : Service
    {
        public override bool IsCritical => true;
        public override void OnReady()
        {
#if !DEBUG
            Program.Client.MessageReceived += Client_MessageReceived;
            Program.Client.MessageUpdated += Client_MessageUpdated;
            Program.Client.ChannelUpdated += Client_ChannelUpdated;
            Program.Client.GuildUpdated += Client_GuildUpdated;
            Catchup().Wait();
#endif
        }

        #region Startup catchup
        async Task Catchup()
        {
            using var DB = Program.Services.GetRequiredService<LogContext>();
            foreach (var guild in Program.Client.Guilds)
            {
                if (guild.Id != 365230804734967840)
                    continue;
                try
                {
                    await Catchup(guild, DB);
                } catch(Exception ex)
                {
                    Program.LogMsg(ex, "Cch-" + guild.Id.ToString());
                }
            }
        }
        async Task Catchup(SocketGuild guild, LogContext DB)
        {
            foreach(var txt in guild.TextChannels)
            {
                try
                {
                    await Catchup(txt, DB);
                }
                catch (Exception ex)
                {
                    Program.LogMsg(ex, $"{guild.Id}-{txt.Id}");
                }
            }
        }
        async Task Catchup(SocketTextChannel txt, LogContext DB)
        {
            bool exit = false;
            do
            {
                var msgs = await txt.GetMessagesAsync(limit: 10).FlattenAsync();
                var ordered = msgs.OrderByDescending(x => x.Id);
                // Highest first, meaning latest message
                foreach (var x in ordered)
                {
                    if (!(x is IUserMessage sm))
                        continue;
                    // x, starting at the latest message in the channel
                    if (DB.Messages.Any(db => db.MessageId == cast(sm.Id)))
                    {
                        exit = true;
                        break;
                    }
                    await AddMessage(sm);
                }
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
            var thr = new Thread(downloadAttachmentThread);
            thr.Start(data);
        }

        void downloadAttachmentThread(object arg)
        {
            if (!(arg is imageData data))
                return;
            Program.LogMsg($"Downloading {data.Attachment.Url}", LogSeverity.Info, "Attch");
            using var cl = new WebClient();
            cl.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
            {
                Program.LogMsg($"Progress {data.Attachment.Url}: {e.ProgressPercentage}%, {e.BytesReceived}");
            };
            var path = Path.Combine(Path.GetTempPath(), $"{data.MessageId}_{data.Attachment.Filename}");
            cl.DownloadFile(data.Attachment.Url, path);
            Program.LogMsg($"Downloaded {data.Attachment.Url}");
            var service = Program.Services.GetRequiredService<LoggingService>();
            var chnl = service.GetChannel(data.Guild, "attachment").Result;
            Program.LogMsg($"Uploading {data.Attachment.Url}");
            var message = chnl.SendFileAsync(path, $"{data.Guild.Id}-{data.MessageId}").Result;
            Program.LogMsg($"Uploaded {data.Attachment.Url}");
            using var DB = Program.Services.GetRequiredService<LogContext>();
            MsgModel dbMsg;
            int tries = 0;
            do
            {
                dbMsg = DB.Messages.AsQueryable().FirstOrDefault(x => x.MessageId == cast(data.MessageId));
                if (dbMsg == null)
                {
                    if (tries > 20)
                    {
                        Program.LogMsg($"Cancel - Could not locate message {data.MessageId} for setting new attachment url.", LogSeverity.Error, "LogAtt");
                        return;
                    }
                    tries++;
                    Program.LogMsg($"{tries:00} - Could not locate message {data.MessageId} for setting new attachment url.", LogSeverity.Warning, "LogAtt");
                    Thread.Sleep(1000 * tries);
                }
            } while (dbMsg == null);
            dbMsg.Attachments = dbMsg.Attachments.Replace(data.Attachment.Url, message.Attachments.First().Url);
            DB.SaveChanges();
            Program.LogMsg($"Completed all {data.Attachment.Url}", LogSeverity.Warning, "Attch");
        }

        #endregion

        async Task AddMessage(IMessage arg)
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
            using var DB = Program.Services.GetRequiredService<LogContext>();
            DB.Contents.Add(content);
            DB.SaveChanges();
            var msg = new MsgModel(umsg);
            msg.ContentId = content.Id;
            DB.Messages.Add(msg);
            await DB.SaveChangesAsync();
            var bg = Console.BackgroundColor;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.White;
            Console.WriteLine($"{getWhere(umsg)}: {arg.Author.Username}: {arg.Content}");
            Console.BackgroundColor = bg;
            foreach(var attch in umsg.Attachments)
            {
                HandleAttachment(attch, guildChannel.Guild, umsg.Id);
            }
        }

        private async Task Client_MessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        {
            if (arg2.Author.IsBot || arg2.Author.IsWebhook)
                return;
            using var DB = Program.Services.GetRequiredService<LogContext>();
            var origMsg = await DB.Messages.AsQueryable().FirstOrDefaultAsync(x => x.MessageId == cast(arg1.Id));
            if (origMsg == null)
                return; // TODO: add the message in
            var currentContent = DB.Contents.First(x => x.Id == origMsg.ContentId);
            if (currentContent.Content == arg2.Content)
                return;
            var newContent = new MsgContent()
            {
                Content = arg2.Content,
                Timestamp = arg2.EditedTimestamp.GetValueOrDefault(DateTimeOffset.Now).DateTime,
                Message = arg2.Id
            };
            await DB.Contents.AddAsync(newContent);
            await DB.SaveChangesAsync();
            origMsg.ContentId = newContent.Id;
            await DB.SaveChangesAsync();
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
            using var DB = Program.Services.GetRequiredService<LogContext>();
            var t = DB.Contents.AsQueryable().Where(x => x.MessageId == cast(message));
            return t.ToList();
        }
        public MsgContent GetLatestContent(ulong message)
        {
            return GetContents(message).LastOrDefault();
        }

        public async Task<List<DbMsg>> GetMessagesAsync(ulong guild, ulong channel, ulong before = ulong.MaxValue, int limit = 25)
        {
            using var DB = Program.Services.GetRequiredService<LogContext>();
            var query = DB.Messages.AsQueryable().Where(x => x.GuildId == cast(guild) && x.ChannelId == cast(channel));
            var msgs = query.AsAsyncEnumerable()
                .Where(x => x.Message < before)
                .OrderByDescending(x => x.Message)
                .Take(limit);
            var result = await msgs.ToListAsync();
            return result.Select(x => new DbMsg(this, x)).ToList();
        }

        public async Task<DbMsg> GetMessageAsync(ulong messageId)
        {
            using var DB = Program.Services.GetRequiredService<LogContext>();
            var model = DB.Messages.AsQueryable().FirstOrDefault(x => x.MessageId == cast(messageId));
            if (model == null)
                return null;
            return new DbMsg(this, model);
        }

        public async Task<List<ReturnedMsg>> GetCombinedMsgs(ulong guild, ulong channel, ulong before = ulong.MaxValue, int limit = 25)
        {
            using var DB = Program.Services.GetRequiredService<LogContext>();
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
                    if(x.Content == null)
                    {
                        x.Content = inDs.Content;
                        var msgContent = new MsgContent()
                        {
                            Message = inDs.Id,
                            Timestamp = (inDs.EditedTimestamp ?? inDs.Timestamp).DateTime,
                            Content = inDs.Content
                        };
                        DB.Contents.Add(msgContent);
                        changes = true;
                    }
                }
            }
            foreach(var ds in dsMessages)
            {
                if (!(ds is IUserMessage umsg))
                    continue;
                if (!DB.Messages.Any(x => x.MessageId == cast(ds.Id)))
                {
                    total.Add(new DiscordMsg(this, umsg));
                    await AddMessage(umsg);
                }
            }
            if (changes)
                await DB.SaveChangesAsync();
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
                ObjectId = arg2.Id,
                Timestamp = DateTime.Now,
                Name = arg2.Name
            };
            using var DB = Program.Services.GetRequiredService<LogContext>();
            DB.Names.Add(stamp);
            await DB.SaveChangesAsync();
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
                ObjectId = chnl1.Id,
                Timestamp = DateTime.Now,
                Name = chnl2.Name
            };
            using var DB = Program.Services.GetRequiredService<LogContext>();
            DB.Names.Add(stamp);
            await DB.SaveChangesAsync();
        }

        public List<NameTimestamps> GetNamesFor(ulong id)
        {
            using var DB = Program.Services.GetRequiredService<LogContext>();
            return Enumerable.Where(DB.Names, x => x.ObjectId == id).ToList();
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
            await AddMessage(arg);
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
