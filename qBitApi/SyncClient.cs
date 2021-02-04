using qBitApi.API;
using qBitApi.Logging;
using qBitApi.REST;
using qBitApi.REST.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace qBitApi
{
    public class SyncClient : IDisposable
    {
        internal qBitApiClient ApiClient { get; set; }
        internal qBittorrentClient Client { get; set; }

        public event Func<Torrent, Task> TorrentUpdated
        {
            add => _torrentUpdated.Add(value);
            remove => _torrentUpdated.Remove(value);
        }
        private AsyncEvent<Func<Torrent, Task>> _torrentUpdated = new AsyncEvent<Func<Torrent, Task>>();
        public event Func<string, Task> TorrentRemoved { add => _torrentRemoved.Add(value); remove => _torrentRemoved.Remove(value); }
        private AsyncEvent<Func<string, Task>> _torrentRemoved = new AsyncEvent<Func<string, Task>>();

        Dictionary<string, Torrent> _torrents { get; set; }
        CancellationTokenSource _cancelToken;
        SemaphoreSlim _stateLock;
        int _interval = 15 * 1000;
        Logger _logger;

        internal SyncClient(qBittorrentClient client)
        {
            _logger = client.LogManager.CreateLogger("Sync");
            Client = client;
            ApiClient = client.ApiClient;
            _torrents = new Dictionary<string, Torrent>();
            _stateLock = new SemaphoreSlim(1, 1);
        }

        internal async Task SetTorrents(params Torrent[] torrents)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _torrents = new Dictionary<string, Torrent>();
                foreach (var x in torrents)
                    _torrents[x.Hash] = x;
            } finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Sets the interval between each sync
        /// </summary>
        /// <param name="interval">Interval, in seconds, between each sync</param>
        public async Task SetInterval(int interval)
        {
            Preconditions.AtLeast(interval, 5000, nameof(interval));
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _interval = interval;
                await _logger.DebugAsync($"Interval updated to {interval}ms").ConfigureAwait(false);
            } finally
            {
                _stateLock.Release();
            }
        }


        private int _rid = 0;
        private async Task sync()
        {
            SyncInfo info;
            await _logger.DebugAsync($"[{_rid}] Fetching sync...").ConfigureAwait(false);
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                info = await ApiClient.SendAsync<SyncInfo>("GET", $"sync/maindata?rid={_rid}");
                _rid = info.rid;
            }
            finally
            {
                _stateLock.Release();
            }
            if (info.torrents != null)
            {
                await _logger.DebugAsync($"[{_rid}] {info.torrents.Count} Torrents updated, updating state and sending events...").ConfigureAwait(false);
                foreach (var keypair in info.torrents)
                {
                    await _stateLock.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        Torrent torrent;
                        if (_torrents.TryGetValue(keypair.Key, out torrent))
                        {
                            torrent.Update(keypair.Value);
                        } else
                        {
                            await _logger.WarningAsync($"[{_rid}] Tracking new torrent '{keypair.Key}'").ConfigureAwait(false);
                            torrent = Torrent.Create(keypair.Value);
                            _torrents[keypair.Key] = torrent;
                        }
                        await _torrentUpdated.InvokeAsync(torrent);
                    }
                    finally
                    {
                        _stateLock.Release();
                    }
                }
            }
            if (info.torrents_removed != null)
            {
                await _logger.DebugAsync($"[{_rid}] {info.torrents_removed.Length} Torrents removed, checking...").ConfigureAwait(false);
                foreach (var hash in info.torrents_removed)
                {
                    await _logger.DebugAsync($"[{_rid}] No longer tracking '{hash}'").ConfigureAwait(false);
                    await _stateLock.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        _torrents.Remove(hash);
                    }
                    finally
                    {
                        _stateLock.Release();
                    }
                    await _torrentRemoved.InvokeAsync(hash).ConfigureAwait(false);
                }
            }
        }

        public bool HasLoaded => _rid > 0;

        public void StartSync()
        {
            StopSync();
            _cancelToken = new CancellationTokenSource();
            new Thread(() =>
            {
                try
                {
                    loop().Wait();
                } catch(AggregateException ex) when(ex.InnerExceptions.Count == 1 && ex.InnerException is TaskCanceledException)
                {
                }
                catch(Exception ex)
                {
                    _logger.ErrorAsync(ex);
                } 
                _cancelToken?.Dispose();
                _logger.DebugAsync("Thread has closed.");
            }).Start();
        }
        async Task loop()
        {
            await _logger.DebugAsync("Syncing has started");
            while (!_cancelToken.IsCancellationRequested)
            {
                await sync();
                await Task.Delay(_interval, _cancelToken.Token);
            }
        }
        public void StopSync()
        {
            _cancelToken?.Cancel();
        }

        #region IDisposable Support
        internal bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _cancelToken?.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

    }
}
