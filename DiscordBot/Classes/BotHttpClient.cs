using Discord;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Classes
{
    public class BotHttpClient
    {
        public static bool ForceDebug { get; set; } = false;
        private HttpClient http;
        protected static SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        public BotHttpClient(HttpClient client, string source = null, bool debug = false, 
            IRateLimiter ratelimiter = null,
            bool storeCookies = false)
        {
            http = client;
            _source = source;
            _debug = debug;
            _ratelimiter = ratelimiter ?? new DefaultRateLimiter();
            _storeCookies = storeCookies;
        }

        private string _source;
        private bool _debug;
        private IRateLimiter _ratelimiter;
        private bool _storeCookies;

        private CookieContainer _cookies = new CookieContainer();

        private DateTimeOffset _lastSent = DateTimeOffset.Now;

        private static int order = 1;

        public HttpHeaders DefaultRequestHeaders { get; set; } = new BotHttpHeaders();

        public BotHttpClient Child(string source, bool debug = false, IRateLimiter rateLimiter = null, bool storeCookies = false)
        {
            var x = new BotHttpClient(http, source, debug, rateLimiter, storeCookies);
            return x;
        }
        public BotHttpClient WithCookie(Cookie cookie)
        {
            _storeCookies = true;
            _cookies.Add(cookie);
            return this;
        }

        string getSource()
        {
            if (_source != null)
                return _source;
            var stack = new StackTrace(2, true);
            foreach(StackFrame frame in stack.GetFrames())
            {
                var meth = frame.GetMethod();
                if (meth == null)
                    continue;
                var cls = frame.GetMethod()?.DeclaringType?.Name ?? "";
                if (cls == "BotHttpClient")
                    continue;
                return $"{cls}::{meth.Name}#{frame.GetFileLineNumber()}";
            }
            return "BotHttp";
        }

        string str(HttpRequestMessage message)
        {
            return $"{message.Method} {message.RequestUri}";
        }
        string str(HttpResponseMessage response)
            => $"{response.RequestMessage.Method} {response.RequestMessage.RequestUri} {response.StatusCode}";

        async Task<string> full(HttpRequestMessage message)
        {
            var builder = new StringBuilder();
            builder.Append($"{message.Method} {message.RequestUri.PathAndQuery} HTTP/{http.DefaultRequestVersion}\n");
            builder.Append($"Host: {message.RequestUri.Host}\n");
            foreach (var h in message.Headers)
                builder.Append($"{h.Key}: {string.Join(", ", h.Value)}\n");
            builder.Append("\n");
            if (message.Content == null)
                return builder.ToString();
            if(message.Content is StringContent content)
            {
                var s = await content.ReadAsStringAsync();
                builder.Append(s);
            } else
            {
                builder.Append($"<content: {message.Content.GetType().Name}>");
            }
            return builder.ToString();
        }

        async Task<string> full(HttpResponseMessage response)
        {
            var builder = new StringBuilder();
            builder.Append($"HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}\n");
            foreach (var header in response.Headers)
                builder.Append($"{header.Key}: {string.Join(", ", header.Value)}\n");
            builder.Append("\n");
            if (response.Content == null)
                return builder.ToString();
            builder.Append($"<content: {response.Content.GetType().Name}>\n");
            var s = await response.Content.ReadAsStringAsync();
            builder.Append(s);
            return builder.ToString();
        }

        public static string LogFolder => Path.Combine(Program.BASE_PATH, "data", "logs", "http", "sent", DateTime.Now.ToString("yyyy-MM-dd"));

        async Task<HttpResponseMessage> InternalSendAsync(HttpRequestMessage message, string source, CancellationToken? token)
        {

            foreach (var defalt in DefaultRequestHeaders)
                message.Headers.TryAddWithoutValidation(defalt.Key, defalt.Value);

            if(_storeCookies)
            {
                var header = _cookies.GetCookieHeader(message.RequestUri);
                message.Headers.Add("Cookie", header);
            }

            int count = (int)DateTime.Now.TimeOfDay.TotalSeconds;
            string fName = $"{count:00000}.txt";
            if (!Directory.Exists(LogFolder))
                Directory.CreateDirectory(LogFolder);
            var path = Path.Combine(LogFolder, fName);
            if(_debug || ForceDebug)
            {
                File.WriteAllText(path, await full(message));
            }
            await _lock.WaitAsync();
            var ct = Program.GetToken();
            await _ratelimiter?.BeforeRequest(message);
            try
            {
                Program.LogDebug(str(message), source);
                var sw = Stopwatch.StartNew();
                var response = await http.SendAsync(message, ct);
                sw.Stop();
                _lastSent = DateTimeOffset.Now;
                Program.LogInfo($"{str(message)} {sw.ElapsedMilliseconds}ms", source);
                if(_debug || ForceDebug)
                {
                    string contents = "\r\n==============================\r\n";
                    contents += await full(response);
                    File.AppendAllText(path, contents);
                }

                if(_storeCookies && response.Headers.TryGetValues("Set-Cookie", out var s))
                {
                    _cookies.SetCookies(message.RequestUri, string.Join(", ", s));
                }

                await _ratelimiter?.AfterRequest(response);

                return response;
            } finally
            {
                _lock.Release();
            }
        }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, string source = null, CancellationToken? token = null)
            => InternalSendAsync(message, source ?? getSource(), token);

        public Task<HttpResponseMessage> GetAsync(Uri uri, string source = null, CancellationToken? token = null)
            => SendAsync(new HttpRequestMessage(HttpMethod.Get, uri), source, token);
        public Task<HttpResponseMessage> GetAsync(string url, string source = null, CancellationToken? token = null)
            => SendAsync(new HttpRequestMessage(HttpMethod.Get, url), source, token);
        public async Task<Stream> GetStreamAsync(string url, string source = null, CancellationToken? token = null)
        {
            var resp = await SendAsync(new HttpRequestMessage(HttpMethod.Get, url), source, token);
            return await resp.Content.ReadAsStreamAsync();
        }

        public Task<HttpResponseMessage> PostAsync(string url, HttpContent content,string source = null, CancellationToken? token = null)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = content;
            return SendAsync(req, source, token);
        }

        public static Task DelayUntil(DateTimeOffset datetime, int maxDelay = -1)
        {
            var intDiff = (int)(DateTimeOffset.Now - datetime).TotalMilliseconds;
            if (intDiff > 0)
            {
                if (maxDelay > 0)
                    intDiff = Math.Min(intDiff, maxDelay);
                return Task.Delay(intDiff);
            }
            return Task.CompletedTask;
        }

        public class DefaultRateLimiter : IRateLimiter
        {
            private Dictionary<string, DateTimeOffset> domainLastSent = new Dictionary<string, DateTimeOffset>();
            private int msDelay;
            public DefaultRateLimiter(int mstimeout = 1000)
            {
                if (mstimeout <= 0) throw new ArgumentException($"Timeout must be greater than zero ms", nameof(mstimeout));
                msDelay = mstimeout;
            }
            public Task AfterRequest(HttpResponseMessage message)
            {
                return Task.CompletedTask;
            }

            public async Task BeforeRequest(HttpRequestMessage message)
            {
                if(domainLastSent.TryGetValue(message.RequestUri.Host, out DateTimeOffset date))
                {
                    var diff = DateTimeOffset.Now - date;
                    int intD = (int)(msDelay - diff.TotalMilliseconds);
                    if(intD > 0)
                    {
                        Program.LogVerbose($"Ratelimiting {message.Method} {message.RequestUri}", "HttpClient");
                        await Task.Delay(intD);
                    }
                }
                domainLastSent[message.RequestUri.Host] = DateTimeOffset.Now;
            }
        }
    }

    public class BotHttpHeaders : HttpHeaders
    {
    }

    public interface IRateLimiter
    {
        public Task BeforeRequest(HttpRequestMessage message);
        public Task AfterRequest(HttpResponseMessage message);

    }

}
