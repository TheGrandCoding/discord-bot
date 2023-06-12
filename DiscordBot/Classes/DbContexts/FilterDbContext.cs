using DiscordBot.Classes;
using DiscordBot.Utils;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Classes.DbContexts
{
    public class FilterDbContext : AbstractDbBase
    {
        private static int _count = 0;
        private static SemaphoreSlim _semaphore = new(1, 1);
        protected override int _lockCount { get => _count; set => _count = value; }
        protected override SemaphoreSlim _lock => _semaphore;

        public DbSet<FilterList> Filters { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.WithSQLConnection("filters", true);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {

        }

        public ValueTask<FilterList> GetFilter(Guid id)
        {
            return WithLock(() => Filters.FindAsync(id));
        }

        public IAsyncEnumerable<FilterList> GetFilters(uint userId)
        {
            return WithLock(() => Filters.AsAsyncEnumerable().Where(x => x.AuthorId == userId));
        }

        public Task DeleteFilter(Guid id)
        {
            return WithLock(async () =>
            {
                var f = await Filters.FindAsync(id);
                Filters.Remove(f);
            });
        }
        public void DeleteFilter(FilterList f)
        {

            WithLock(() =>
            {
                Filters.Remove(f);
            });
        }
    }
    public class FilterList
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public Guid Id { get; set; }

        public uint AuthorId { get; set; }

        [MaxLength(32)]
        public string Name { get; set; }
        [MaxLength(int.MaxValue)]
        public string Text { get; set; }
        [MaxLength(int.MaxValue)]
        public string AutoAddTemplate { get; set; }

    }
}
