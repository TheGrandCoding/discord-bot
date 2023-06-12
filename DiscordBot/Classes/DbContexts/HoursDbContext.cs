using DiscordBot.Services;
using DiscordBot.Utils;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Classes.DbContexts
{
    public class HoursDbContext : AbstractDbBase
    {
        private static int _count = 0;
        private static SemaphoreSlim _semaphore = new(1, 1);
        protected override int _lockCount { get => _count; set => _count = value; }
        protected override SemaphoreSlim _lock => _semaphore;
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.WithSQLConnection("hours");
        }
        public DbSet<HoursSettings> Settings { get; set; }
        public DbSet<HoursEntry> Entries { get; set; }

        public async Task AddSetting(string settingId, ulong id,
            DateTime startDate, DateTime endDate,
            string startTime, string endTime,
            double breakTime,
            double rate, double overtimeRate)
        {
            var _id = unchecked((long)id);
            var s = new HoursSettings()
            {
                Id = settingId,
                UserId = _id,
                StartDate = startDate,
                EndDate = endDate,
                ExpectedStartTime = startTime,
                ExpectedEndTime = endTime,
                ExpectedBreak = breakTime,
                NormalRate = rate,
                OvertimeRate = overtimeRate
            };
            await Settings.AddAsync(s);
        }
        public async Task AddEntry(string settingId, ulong id, double hours, double overtime, double? breakTime = null, DateTime? date = null)
        {
            var entry = new HoursEntry()
            {
                Date = date.GetValueOrDefault(DateTime.UtcNow),
                SettingId = settingId,
                NormalHours = hours,
                OvertimeHours = overtime,
                BreakHours = breakTime.GetValueOrDefault(1),
                UserId = unchecked((long)id)
            };
            await Entries.AddAsync(entry);
        }

        public IReadOnlyCollection<HoursSettings> GetSettings(ulong id)
        {
            var _id = unchecked((long)id);
            return Settings.Where(x => x.UserId == _id).ToArray();
        }

        public IReadOnlyCollection<HoursEntry> GetEntries(ulong id, string settingId = null, DateTime? after = null, DateTime? before = null)
        {
            var _id = unchecked((long)id);
            HoursEntry[] entries;
            if (settingId != null)
                entries = Entries.Where(x => x.UserId == _id && x.SettingId == settingId).ToArray();
            else
                entries = Entries.Where(x => x.UserId == _id).ToArray();

            if (after.HasValue)
                entries = entries.Where(x => x.Date >= after.Value).ToArray();
            if (before.HasValue)
                entries = entries.Where(x => x.Date < before.Value).ToArray();

            return entries;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HoursSettings>()
                .HasKey(x => new { x.Id, x.UserId });
            modelBuilder.Entity<HoursSettings>()
                .Property(x => x.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<HoursEntry>()
                .HasKey(x => new { x.SettingId, x.UserId, x.Date });
        }
    }

}
