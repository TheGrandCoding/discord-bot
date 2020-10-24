using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class LogContext : DbContext
    {
        public LogContext([NotNull] DbContextOptions options) : base(options)
        {
        }

        public DbSet<MsgModel> Messages { get; set; }
        public DbSet<NameTimestamps> Names { get; set; }
        public DbSet<MsgContent> Contents { get; set; }

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

        public bool IsDeleted { get; set; }
    }
    public class DiscordMsg : ReturnedMsg
    {
        public DiscordMsg(MsgService s, IUserMessage message) : base(s)
        {
            Id = message.Id;
            Content = message.Content;
            Author = message.Author;
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
        public LogContext DB { get; set; }
        public override void OnReady()
        {
            DB = Program.Services.GetRequiredService<LogContext>();
            Program.Client.MessageReceived += Client_MessageReceived;
            Program.Client.MessageUpdated += Client_MessageUpdated;
            Program.Client.ChannelUpdated += Client_ChannelUpdated;
            Program.Client.GuildUpdated += Client_GuildUpdated;
        }

        private async Task Client_MessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        {
            if (arg2.Author.IsBot || arg2.Author.IsWebhook)
                return;
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
            var t = DB.Contents.AsQueryable().Where(x => x.MessageId == cast(message));
            return t.ToList();
        }
        public MsgContent GetLatestContent(ulong message)
        {
            return GetContents(message).LastOrDefault();
        }

        public async Task<List<DbMsg>> GetMessagesAsync(ulong guild, ulong channel, ulong before = ulong.MaxValue, int limit = 25)
        {
            var query = DB.Messages.AsQueryable().Where(x => x.GuildId == cast(guild) && x.ChannelId == cast(channel));
            var msgs = query.AsAsyncEnumerable()
                .Where(x => x.Message < before)
                .OrderByDescending(x => x.Message)
                .Take(limit);
            var result = await msgs.ToListAsync();
            return result.Select(x => new DbMsg(this, x)).ToList();
        }

        public async Task<List<ReturnedMsg>> GetCombinedMsgs(ulong guild, ulong channel, ulong before = ulong.MaxValue, int limit = 25)
        {
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
            foreach(var x in total)
            {
                if (!dsMessages.Any(ds => ds.Id == x.Id))
                    x.IsDeleted = true;
            }
            bool changes = false;
            foreach(var ds in dsMessages)
            {
                if (!total.Any(x => x.Id == ds.Id))
                {
                    total.Add(new DiscordMsg(this, (IUserMessage)ds));
                    var content = new MsgContent()
                    {
                        Message = ds.Id,
                        Timestamp = (ds.EditedTimestamp ?? ds.Timestamp).DateTime,
                        Content = ds.Content
                    };
                    DB.Contents.Add(content);
                    await DB.SaveChangesAsync();
                    var toStore = new MsgModel((IUserMessage)ds);
                    toStore.ContentId = content.Id;
                    DB.Messages.Add(toStore);
                    changes = true;
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
            DB.Names.Add(stamp);
            await DB.SaveChangesAsync();
        }

        public List<NameTimestamps> GetNamesFor(ulong id)
        {
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
            if (!(arg is SocketUserMessage umsg))
                return;
            if (!(arg.Channel is SocketGuildChannel))
                return;
            if (arg.Author.IsBot || arg.Author.IsWebhook)
                return;
            var content = new MsgContent()
            {
                Message = umsg.Id,
                Content = umsg.Content,
                Timestamp = umsg.Timestamp.DateTime,
            };
            DB.Contents.Add(content);
            DB.SaveChanges();
            var msg = new MsgModel(umsg);
            msg.ContentId = content.Id;
            DB.Messages.Add(msg);
            await DB.SaveChangesAsync();
            Console.WriteLine($"{getWhere(umsg)}: {arg.Author.Username}: {arg.Content}");
        }

        string getWhere(SocketUserMessage m)
        {
            var s = "{0}/{1}#{2}";
            var guildName = "";
            var categoryName = "";
            if (m.Channel is SocketTextChannel g)
            {
                guildName = g.Guild.Name;
                categoryName = g.Category?.Name ?? "";
            }
            return string.Format(s, guildName, categoryName, m.Channel.Name);
        }
    }
}
