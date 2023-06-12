using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Classes.DbContexts
{
    public abstract class AbstractDbBase : DbContext
    {
        protected abstract override void OnConfiguring(DbContextOptionsBuilder options);
        protected abstract override void OnModelCreating(ModelBuilder builder);

        public int Id { get; private set; }
        public string Reason { get; private set; }
        private static int _id = 0;
        public void SetReason(string reason)
        {
            if (Id == 0)
            {
                Id = System.Threading.Interlocked.Increment(ref _id);
                Program.LogDebug($"Created DB {Id}/{reason}", this.GetType().Name);
                Reason = reason;
            }
            else
            {
                Reason = (Reason ?? "") + "+" + reason;
                Program.LogDebug($"Re-used DB {Id}/{Reason}", this.GetType().Name);
            }
        }
        public override void Dispose()
        {
            Program.LogWarning($"Disposing DB {Id}/{Reason}", this.GetType().Name);
            base.Dispose();
        }

        protected abstract int _lockCount { get; set; }
        protected abstract SemaphoreSlim _lock { get; }
        public T WithLock<T>(Func<T> func)
        {
            try
            {
                if (_lockCount == 0)
                {
                    _lock.Wait();
                }
                _lockCount++;
                var result = func();
                return result;
            }
            finally
            {
                _lockCount--;
                if (_lockCount == 0)
                {
                    _lock.Release();
                }
            }
        }
        public void WithLock(Action action)
        {
            try
            {
                if (_lockCount == 0)
                {
                    _lock.Wait();
                }
                _lockCount++;
                action();
            }
            finally
            {
                _lockCount--;
                if (_lockCount == 0)
                {
                    _lock.Release();
                }
            }
        }


    }
}
