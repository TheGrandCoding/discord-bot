using Newtonsoft.Json;
using qBitApi.Logging;
using qBitApi.REST;
using qBitApi.REST.Entities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace qBitApi
{
    public class qBittorrentClient : IDisposable
    {
        public event Func<LogMessage, Task> Log { add { _logEvent.Add(value); } remove { _logEvent.Remove(value); } }
        internal readonly AsyncEvent<Func<LogMessage, Task>> _logEvent = new AsyncEvent<Func<LogMessage, Task>>();

        public event Func<Task> LoggedIn { add { _loggedInEvent.Add(value); } remove { _loggedInEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Task>> _loggedInEvent = new AsyncEvent<Func<Task>>();
        public event Func<Task> LoggedOut { add { _loggedOutEvent.Add(value); } remove { _loggedOutEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Task>> _loggedOutEvent = new AsyncEvent<Func<Task>>();

        private readonly SemaphoreSlim _stateLock;
        private bool _isFirstLogin, _isDisposed;
        private CancellationTokenSource _cancelToken;

        internal qBitApiClient ApiClient;
        private SyncClient SyncClient;
        internal readonly Logger _restLogger;
        internal LogManager LogManager { get; }

        public LoginState LoginState { get; private set; }

        public qBittorrentClient(Uri host, string userAgent = "qbit-api-core", RetryMode retry = RetryMode.AlwaysRetry, LogSeverity minimum = LogSeverity.Info)
        {
            Preconditions.NotNull(host, nameof(host));
            LogManager = new LogManager(minimum);
            ApiClient = new qBitApiClient(userAgent, retry);
            ApiClient.SetBaseUrl(Path.Combine(host.AbsoluteUri, $"api/v{qBitApiConfig.APIVersion}"));
            LogManager.Message += async msg => await _logEvent.InvokeAsync(msg).ConfigureAwait(false);
            ApiClient.SentRequest += async (method, endpoint, millis) => 
            { await _restLogger.VerboseAsync($"{method} {endpoint}: {millis} ms").ConfigureAwait(false); };
            _restLogger = LogManager.CreateLogger("Rest");
            _stateLock = new SemaphoreSlim(1, 1);
            _isFirstLogin = false;
            _cancelToken = new CancellationTokenSource();
        }

        internal virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    ApiClient?.Dispose();
                    _stateLock?.Dispose();
                    _cancelToken?.Dispose();
                    SyncClient?.Dispose();
                }
                _isDisposed = true;
            }
        }
        public void Dispose() => Dispose(true);

        public async Task LoginAsync(string username, string password)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await LoginInternalAsync(username, password).ConfigureAwait(false);
            }
            finally { _stateLock.Release(); }
        }
        internal virtual async Task LoginInternalAsync(string username, string password)
        {
            if (_isFirstLogin)
            {
                _isFirstLogin = false;
                await LogManager.WriteInitialLog().ConfigureAwait(false);
            }

            if (LoginState != LoginState.LoggedOut)
                await LogoutInternalAsync().ConfigureAwait(false);
            LoginState = LoginState.LoggingIn;

            try
            {
                await ApiClient.LoginAsync(username, password).ConfigureAwait(false);
                await OnLoginAsync(username).ConfigureAwait(false);
                LoginState = LoginState.LoggedIn;
            }
            catch
            {
                await LogoutInternalAsync().ConfigureAwait(false);
                throw;
            }

            await _loggedInEvent.InvokeAsync().ConfigureAwait(false);
        }
        internal virtual Task OnLoginAsync(string username)
            => Task.Delay(0);
        
        public async Task LogoutAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await LogoutInternalAsync().ConfigureAwait(false);
            }
            finally { _stateLock.Release(); }
        }
        internal virtual async Task LogoutInternalAsync()
        {
            if (LoginState == LoginState.LoggedOut) return;
            LoginState = LoginState.LoggingOut;

            await ApiClient.LogoutAsync().ConfigureAwait(false);

            await OnLogoutAsync().ConfigureAwait(false);
            LoginState = LoginState.LoggedOut;

            await _loggedOutEvent.InvokeAsync().ConfigureAwait(false);
        }
        internal virtual Task OnLogoutAsync()
            => Task.Delay(0);


        #region Application
        public async Task<Version> GetApplicationVersionAsync()
        {
            var version = await ApiClient.GetApplicationVersionAsync().ConfigureAwait(false);
            return Version.Parse(version[1..]);
        }
        public async Task<Version> GetWebApiVersion()
        {
            var version = await ApiClient.GetWebApiVersion().ConfigureAwait(false);
            return Version.Parse(version);
        }
        public async Task<BuildInfo> GetBuildInfoAsync()
        {
            var model = await ApiClient.GetBuildInfoAsync().ConfigureAwait(false);
            return BuildInfo.Create(model);
        }
        #endregion

        #region Torrent Management
        public async Task<IReadOnlyCollection<Torrent>> GetTorrentListAsync(
            TorrentState? filter = null,
            string category = null,
            string sort = null,
            bool? reverse = null,
            int? limit = null,
            int? offset = null,
            params string[] hashes)
        {
            var models = await ApiClient.GetTorrentList(filter, category, sort, reverse, limit, offset, hashes).ConfigureAwait(false);
            return models.Select(x => Torrent.Create(x)).ToImmutableArray();
        }
        #endregion

        #region Sync
        private async Task<API.SyncInfo> GetSyncInfo(int rid)
        {
            return await ApiClient.GetSync(rid).ConfigureAwait(false);
        }

        public async Task<SyncClient> GetSyncClient()
        {
            if(SyncClient == null)
            {
                SyncClient = new SyncClient(this);
            }
            return SyncClient;
        }
        #endregion

        #region Torrent Management
        public async Task<IReadOnlyCollection<TorrentFile>> GetTorrentContents(string hash)
        {
            var files = await ApiClient.GetTorrentContents(hash).ConfigureAwait(false);
            return files.Select(x => TorrentFile.Create(x)).ToArray();
        }

        /// <summary>
        /// Pauses torrents
        /// </summary>
        /// <param name="hashes">Hashes of torrents to pause, or empty to pause all torrents</param>
        public async Task PauseTorrents(params string[] hashes)
        {
            await ApiClient.PauseTorrents(hashes).ConfigureAwait(false);
        }

        public async Task ResumeTorrents(params string[] hashes)
        {
            await ApiClient.ResumeTorrents(hashes).ConfigureAwait(false);
        }

        public async Task DeleteTorrents(bool deleteFiles, params string[] hashes)
        {
            await ApiClient.DeleteTorrents(hashes, deleteFiles).ConfigureAwait(false);
        }

        public async Task RecheckTorrents(params string[] hashes)
        {
            await ApiClient.RecheckTorrents(hashes).ConfigureAwait(false);
        }

        public async Task AddNewTorrent(Action<qBitApi.API.PostNewTorrent> action)
        {
            var pnt = new API.PostNewTorrent();
            action(pnt);
            if ((pnt.Urls == null || pnt.Urls.Length == 0) && string.IsNullOrWhiteSpace(pnt.TorrentFilePath))
                throw new InvalidOperationException("One of Urls and TorrentFilePath is required");
            foreach(var url in (pnt.Urls ?? new string[0]))
            {
                Preconditions.NotNullOrWhitespace(url, nameof(url));
                if(!url.StartsWith("magnet:"))
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                        throw new ArgumentException("Url not valid");
                }
            }
            await ApiClient.AddNewTorrent(pnt).ConfigureAwait(false);
        }

        #endregion
    }
}
