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

namespace DiscordBot.Services
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
    public class FilterListService : Service
    {
        string BaseDir => Path.Combine(Program.BASE_PATH, "data", "filters");

        public string GetDirectory(ulong userId)
        {
            var folder = Path.Combine(BaseDir, userId.ToString());
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }
        public string GetFilePath(ulong userId, string filterId, string kind = "txt")
        {
            var folder = GetDirectory(userId);
            return Path.Combine(folder, Program.GetSafePath(filterId + $".{kind}"));
        }

        bool openFile(ulong userId, string filterId, bool readOnly, out FileStream stream, string kind = "txt")
        {
            try
            {
                if (readOnly)
                    stream = File.OpenRead(GetFilePath(userId, filterId, kind));
                else
                    stream = File.Open(GetFilePath(userId, filterId, kind), FileMode.Create, FileAccess.Write);
                return true;
            } catch(FileNotFoundException)
            {
                stream = null;
                return false;
            }
        }

        IEnumerable<ulong> getUserIds()
        {
            foreach(var userDir in Directory.EnumerateDirectories(BaseDir))
            {
                yield return ulong.Parse(Path.GetFileName(userDir));
            }
        }

        public bool TryReadTemplate(ulong userId, string filterId, out FileStream fs)
            => openFile(userId, filterId, true, out fs, "template");
        public bool TryOpenRead(string filterId, out FileStream fs)
        {
            foreach(var userId in getUserIds())
            {
                if (TryOpenRead(userId, filterId, out fs))
                    return true;
            }
            fs = null;
            return false;
        }
        public bool TryOpenRead(ulong userId, string filterId, out FileStream stream)
            => openFile(userId, filterId, true, out stream);

        public override void OnReady(IServiceProvider services)
        {
            var userIds = getUserIds().ToList();
            if (userIds.Count == 0) return;

            var filterDb = services.GetDb<FilterDbContext>("FilterConvert");
            foreach (var userId in userIds)
            {
                var folder = GetDirectory(userId);
                foreach (var file in Directory.EnumerateFiles(folder, "*.txt"))
                {
                    var oldId = Path.GetFileNameWithoutExtension(file);
                    var existing = filterDb.Filters.FirstOrDefault(x => x.Name == oldId);
                    if (existing != null) continue;
                    var newFilter = new FilterList()
                    {
                        AuthorId = (uint)userId,
                        Name = oldId,
                        AutoAddTemplate = "",
                        Text = ""
                    };

                    FileStream fs = null;
                    try
                    {
                        if (TryOpenRead(oldId, out fs))
                        {
                            using var reader = new StreamReader(fs);
                            newFilter.Text = reader.ReadToEnd();
                        }
                    } finally
                    {
                        fs?.Dispose();
                    }

                    try
                    {
                        if(TryReadTemplate(userId, oldId, out fs))
                        {
                            using var reader = new StreamReader(fs);
                            newFilter.AutoAddTemplate = reader.ReadToEnd();
                        }
                    } finally
                    {
                        fs?.Dispose();
                    }

                    filterDb.Filters.Add(newFilter);

                }
            }
            filterDb.SaveChanges();
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
