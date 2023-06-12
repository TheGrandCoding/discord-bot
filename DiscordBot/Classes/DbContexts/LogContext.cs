using DiscordBot.Services;
using DiscordBot.Utils;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace DiscordBot.Classes.DbContexts
{
    public class LogContext : AbstractDbBase
    {
        private static int _count = 0;
        private static SemaphoreSlim _semaphore = new(1, 1);
        protected override int _lockCount { get => _count; set => _count = value; }
        protected override SemaphoreSlim _lock => _semaphore;


        public DbSet<MsgModel> Messages { get; set; }
        public DbSet<NameTimestamps> Names { get; set; }
        public DbSet<MsgContent> Contents { get; set; }
        public DbSet<StatusLog> Status { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
#if WINDOWS
            options.WithSQLConnection("BotLog");
#else
            options.WithSQLConnection("botData");
#endif
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasCharSet("utf8mb4");

            modelBuilder.Entity<MsgModel>()
                .HasKey(m => new { m.GuildId, m.ChannelId, m.MessageId });
            modelBuilder.Entity<NameTimestamps>()
                .HasKey(m => new { m.ObjectId, m.Timestamp });
            modelBuilder.Entity<MsgModel>()
                .Property(x => x.MessageId);
            modelBuilder.Entity<MsgContent>()
                .Property(x => x.Id)
                .UseIdentityColumn();
            modelBuilder.Entity<StatusLog>()
                .HasKey(x => new { x.AuthorId, x.ChangedAt });
        }
    }
}
