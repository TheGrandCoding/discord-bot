using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using qBitApi.API;
using qBitApi.REST.Net;
using qBitApi.REST.Net.Converters;
using qBitApi.REST.Net.Requests;
using qBitApi.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace qBitApi.REST
{
    internal class qBitApiClient : IDisposable
    {
        public event Func<string, string, double, Task> SentRequest { add { _sentRequestEvent.Add(value); } remove { _sentRequestEvent.Remove(value); } }
        private readonly AsyncEvent<Func<string, string, double, Task>> _sentRequestEvent = new AsyncEvent<Func<string, string, double, Task>>();

        protected readonly JsonSerializer _serializer;
        protected readonly SemaphoreSlim _stateLock;
        protected bool _isDisposed;
        private CancellationTokenSource _loginCancelToken;
        public RetryMode DefaultRetryMode { get; }
        public string UserAgent { get; }
        public LoginState LoginState { get; private set; }
        internal RestClient RestClient { get; private set; }

        public qBitApiClient(string userAgent, RetryMode defaultRetryMode = RetryMode.AlwaysRetry,
            JsonSerializer serializer = null)
        {
            UserAgent = userAgent;
            DefaultRetryMode = DefaultRetryMode;
            _serializer = serializer ?? new JsonSerializer() { ContractResolver = new ApiContractResolver() };
            _stateLock = new SemaphoreSlim(1, 1);
        }
        
        /// <exception cref="ArgumentException">Unknown OAuth token type.</exception>
        internal void SetBaseUrl(string baseUrl)
        {
            RestClient?.Dispose();
            RestClient = new RestClient(baseUrl);
            RestClient.SetHeader("accept", "*/*");
            RestClient.SetHeader("user-agent", UserAgent);
        }
        internal virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _loginCancelToken?.Dispose();
                    RestClient?.Dispose();
                    _stateLock?.Dispose();
                }
                _isDisposed = true;
            }
        }
        public void Dispose() => Dispose(true);

        

        static int nextId = 0;
        public DateTimeOffset LastAttemptAt { get; private set; }
        private async Task<Stream> SendAsync(RestRequest request)
        {
            int id = Interlocked.Increment(ref nextId);
#if DEBUG_LIMITS
            Debug.WriteLine($"[{id}] Start");
#endif
            LastAttemptAt = DateTimeOffset.UtcNow;
            while (true)
            {

#if DEBUG_LIMITS
                Debug.WriteLine($"[{id}] Sending...");
#endif
                try
                {
                    var response = await request.SendAsync().ConfigureAwait(false);

                    if (response.StatusCode < (HttpStatusCode)200 || response.StatusCode >= (HttpStatusCode)300)
                    {
                        switch (response.StatusCode)
                        {
                            case HttpStatusCode.BadGateway: //502
#if DEBUG_LIMITS
                                Debug.WriteLine($"[{id}] (!) 502");
#endif
                                if ((request.Options.RetryMode & RetryMode.Retry502) == 0)
                                    throw new HttpException(HttpStatusCode.BadGateway, request, null);

                                continue; //Retry
                            default:
                                int? code = null;
                                string reason = null;
                                if (response.Stream != null)
                                {
                                    try
                                    {
                                        using (var reader = new StreamReader(response.Stream))
                                        using (var jsonReader = new JsonTextReader(reader))
                                        {
                                            var json = JToken.Load(jsonReader);
                                            try { code = json.Value<int>("code"); } catch { };
                                            try { reason = json.Value<string>("message"); } catch { };
                                        }
                                    }
                                    catch { }
                                }
                                throw new HttpException(response.StatusCode, request, code, reason);
                        }
                    }
                    else
                    {
#if DEBUG_LIMITS
                        Debug.WriteLine($"[{id}] Success");
#endif
                        return response.Stream;
                    }
                }
                //catch (HttpException) { throw; } //Pass through
                catch (TimeoutException)
                {
#if DEBUG_LIMITS
                    Debug.WriteLine($"[{id}] Timeout");
#endif
                    if ((request.Options.RetryMode & RetryMode.RetryTimeouts) == 0)
                        throw;

                    await Task.Delay(500).ConfigureAwait(false);
                    continue; //Retry
                }
                /*catch (Exception)
                {
#if DEBUG_LIMITS
                    Debug.WriteLine($"[{id}] Error");
#endif
                    if ((request.Options.RetryMode & RetryMode.RetryErrors) == 0)
                        throw;
                    await Task.Delay(500);
                    continue; //Retry
                }*/
                finally
                {
#if DEBUG_LIMITS
                    Debug.WriteLine($"[{id}] Stop");
#endif
                }
            }
        }

        private async Task<Stream> SendInternalAsync(RestRequest request)
        {
            if (!request.Options.IgnoreState)
                CheckState();
            if (request.Options.RetryMode == null)
                request.Options.RetryMode = DefaultRetryMode;

            var stopwatch = Stopwatch.StartNew();
            var responseStream = await SendAsync(request).ConfigureAwait(false);
            stopwatch.Stop();

            double milliseconds = ToMilliseconds(stopwatch);
            await _sentRequestEvent.InvokeAsync(request.Method, request.Endpoint, milliseconds).ConfigureAwait(false);

            return responseStream;
        }

        public async Task SendAsync(string method, string endpoint, RequestOptions options = null)
        {
            options ??= RequestOptions.Default;
            var request = new RestRequest(RestClient, method, endpoint, options);
            await SendInternalAsync(request).ConfigureAwait(false);
        }
        public async Task SendJsonAsync(string method, string endpoint, object payload, RequestOptions options)
        {
            string json = payload != null ? SerializeJson(payload) : null;
            var request = new JsonRestRequest(RestClient, method, endpoint, json, options);
            await SendInternalAsync(request).ConfigureAwait(false);
        }
        public async Task SendMultipartAsync(string method, string endpoint, IReadOnlyDictionary<string, object> multipartArgs, RequestOptions options)
        {
            var request = new MultipartRestRequest(RestClient, method, endpoint, multipartArgs, options);
            await SendInternalAsync(request).ConfigureAwait(false);
        }
        public async Task<TResponse> SendAsync<TResponse>(string method, string endpoint, RequestOptions options = null)
        {
            options ??= RequestOptions.Default;
            var request = new RestRequest(RestClient, method, endpoint, options);
            return Deserialize<TResponse>(await SendInternalAsync(request).ConfigureAwait(false));
        }
        public async Task<TResponse> SendJsonAsync<TResponse>(string method, string endpoint, object payload, RequestOptions options)
        {
            string json = payload != null ? SerializeJson(payload) : null;
            var request = new JsonRestRequest(RestClient, method, endpoint, json, options);
            return Deserialize<TResponse>(await SendInternalAsync(request).ConfigureAwait(false));
        }
        public async Task<TResponse> SendMultipartAsync<TResponse>(string method, string endpoint, IReadOnlyDictionary<string, object> multipartArgs, RequestOptions options)
        {
            var request = new MultipartRestRequest(RestClient, method, endpoint, multipartArgs, options);
            return Deserialize<TResponse>(await SendInternalAsync(request).ConfigureAwait(false));
        }

        #region Auth 
        public async Task LoginAsync(string username, string password)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await LoginInternalAsync(username, password).ConfigureAwait(false);
            }
            finally
            {
                _stateLock.Release();
            }
        }
        private async Task LoginInternalAsync(string username, string password)
        {
            if (LoginState != LoginState.LoggedOut)
                await LogoutInternalAsync().ConfigureAwait(false);
            LoginState = LoginState.LoggingIn;

            _loginCancelToken?.Dispose();
            _loginCancelToken = new CancellationTokenSource();

            RestClient.SetCancelToken(_loginCancelToken.Token);


            var args = new Dictionary<string, object>();
            args["username"] = username;
            args["password"] = password;
            await SendMultipartAsync("POST", "auth/login", new ReadOnlyDictionary<string, object>(args), new RequestOptions()
            {
                IgnoreState = true
            }).ConfigureAwait(false);

            LoginState = LoginState.LoggedIn;
        }
        public async Task LogoutAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await LogoutInternalAsync().ConfigureAwait(false);
            }
            finally { _stateLock.Release(); }
        }
        private async Task LogoutInternalAsync()
        {
            if (LoginState == LoginState.LoggedOut) return;
            LoginState = LoginState.LoggingOut;

            await SendAsync("POST", "auth/logout", new RequestOptions()
            {
                IgnoreState = true
            }).ConfigureAwait(false);

            try { _loginCancelToken?.Cancel(false); }
            catch { }

            RestClient.SetCancelToken(CancellationToken.None);
            LoginState = LoginState.LoggedOut;
        }

        #endregion

        #region Application
        public async Task<string> GetApplicationVersionAsync()
        {
            return await SendAsync<string>("GET", "app/version").ConfigureAwait(false); ;
        }
        public async Task<string> GetWebApiVersion()
        {
            return await SendAsync<string>("GET", "app/webapiVersion").ConfigureAwait(false);
        }

        public async Task<BuildInfo> GetBuildInfoAsync()
        {
            return await SendAsync<BuildInfo>("GET", "app/buildInfo").ConfigureAwait(false);
        }
        
        public async Task ShutdownAsync()
        {
            await SendAsync("POST", "app/shutdown").ConfigureAwait(false);
        }

        public async Task<AppPreferences> GetApplicationPreferences()
        {
            return await SendAsync<AppPreferences>("GET", "app/preferences").ConfigureAwait(false);
        }

        public async Task<string> GetDefaultSavePath()
        {
            return await SendAsync<string>("GET", "app/defaultSavePath").ConfigureAwait(false);
        }
        #endregion
        #region Torrent Management
        public async Task<ListTorrentInfo[]> GetTorrentList(
            TorrentState? filter = null,
            string category = null,
            string sort = null,
            bool? reverse = null,
            int? limit = null,
            int? offset = null,
            params string[] hashes)
        {
            var url = new UrlBuilder("torrents/info");
            if(filter.HasValue)
                url["filter"] = Enum.GetName(typeof(TorrentState), filter.Value);
            if (category != null)
                url["category"] = category;
            if (sort != null)
                url["sort"] = sort;
            if (reverse.HasValue)
                url["reverse"] = reverse.Value.ToString();
            if (limit.HasValue)
                url["limit"] = limit.Value.ToString();
            if (offset.HasValue)
                url["offset"] = offset.Value.ToString();
            if(hashes.Length > 0)
                url["hashes"] = string.Join("|", hashes);
            return await SendAsync<ListTorrentInfo[]>("GET", url.ToString()).ConfigureAwait(false);
        }
        #endregion

        #region Helpers

        /// <exception cref="InvalidOperationException">Client is not logged in.</exception>
        protected void CheckState()
        {
            if (LoginState != LoginState.LoggedIn)
                throw new InvalidOperationException("Client is not logged in.");
        }
        protected static double ToMilliseconds(Stopwatch stopwatch) => Math.Round((double)stopwatch.ElapsedTicks / (double)Stopwatch.Frequency * 1000.0, 2);
        protected string SerializeJson(object value)
        {
            var sb = new StringBuilder(256);
            using (TextWriter text = new StringWriter(sb, CultureInfo.InvariantCulture))
            using (JsonWriter writer = new JsonTextWriter(text))
                _serializer.Serialize(writer, value);
            return sb.ToString();
        }
        protected T Deserialize<T>(Stream jsonStream)
        {
            using TextReader text = new StreamReader(jsonStream);
            if (typeof(T) == typeof(string))
                return (dynamic)text.ReadToEnd();
            using (JsonTextReader reader = new JsonTextReader(text))
                return _serializer.Deserialize<T>(reader);
        }

        #endregion Helpers
    }
}
