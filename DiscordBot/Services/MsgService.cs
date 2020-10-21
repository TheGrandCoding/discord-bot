using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace DiscordBot.Services
{
    public class LogContext : DbContext
    {
        public LogContext([NotNull] DbContextOptions options) : base(options)
        {
        }

        public DbSet<MsgModel> Messages { get; set; }
        public DbSet<NameTimestamps> Names { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MsgModel>()
                .HasKey(m => new { m.GuildId, m.ChannelId, m.MessageId });
            modelBuilder.Entity<NameTimestamps>()
                .HasKey(m => new { m.ObjectId, m.Timestamp });
        }
    }

    public class MsgModel
    {
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public ulong AuthorId { get; set; }
        public string Content { get; set; }
        public string Attachments { get; set; }

        public MsgModel(SocketUserMessage message)
        {
            GuildId = ((IGuildChannel)message.Channel).GuildId;
            ChannelId = message.Channel.Id;
            MessageId = message.Id;
            AuthorId = message.Author.Id;
            Content = message.Content;
            Attachments = string.Join(',', System.Linq.Enumerable.Select(message.Attachments, x => x.Url));
        }

        public MsgModel() { }
    }

    public class NameTimestamps
    {
        public ulong ObjectId { get; set; }
        public DateTime Timestamp { get; set; }

        public string Name { get; set; }
    }



    public class MsgService : Service
    {
        public LogContext DB { get; set; }
        public override void OnReady()
        {
            DB = Program.Services.GetRequiredService<LogContext>();
            Program.Client.MessageReceived += Client_MessageReceived;
            Program.Client.ChannelUpdated += Client_ChannelUpdated;
            Program.Client.GuildUpdated += Client_GuildUpdated;
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
            var msg = new MsgModel(umsg);
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
