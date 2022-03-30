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
        private HttpClient http;
        protected static SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        public BotHttpClient(HttpClient client, string source = null, bool debug = false, 
            int ratelimitMs = -1,
            bool storeCookies = false)
        {
            http = client;
            _source = source;
            _debug = debug;
            _ratelimit = ratelimitMs;
            _storeCookies = storeCookies;
        }

        private string _source;
        private bool _debug;
        private int _ratelimit;
        private bool _storeCookies;

        private CookieContainer _cookies = new CookieContainer();

        private DateTimeOffset _lastSent = DateTimeOffset.Now;

        private static int order = 1;

        public HttpHeaders DefaultRequestHeaders { get; set; } = new BotHttpHeaders();

        public BotHttpClient Child(string source, bool debug = false, int ratelimit = -1, bool storeCookies = false)
        {
            var x = new BotHttpClient(http, source, debug, ratelimit, storeCookies);
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

            int count = Interlocked.Increment(ref order);
            string fName = $"{count:0000}.txt";
            if (!Directory.Exists(LogFolder))
                Directory.CreateDirectory(LogFolder);
            var path = Path.Combine(LogFolder, fName);
            if(_debug)
            {
                File.WriteAllText(path, await full(message));
            }
            await _lock.WaitAsync();
            var ct = Program.GetToken();
            if(_ratelimit > 0)
            {
                var diff = (DateTimeOffset.Now - _lastSent).TotalMilliseconds;
                var wait = (int)(_ratelimit - diff);
                if (wait > 0)
                {
                    Program.LogWarning($"{wait}ms ratelimit invoked on {str(message)}", source);
                    await Task.Delay(wait, ct);
                }
            }
            try
            {
                Program.LogDebug(str(message), source);
                var sw = Stopwatch.StartNew();
                var response = await http.SendAsync(message, ct);
                sw.Stop();
                _lastSent = DateTimeOffset.Now;
                Program.LogInfo($"{str(message)} {sw.ElapsedMilliseconds}ms", source);
                if(_debug)
                {
                    string contents = "\r\n==============================\r\n";
                    contents += await full(response);
                    File.AppendAllText(path, contents);
                }

                if(_storeCookies && response.Headers.TryGetValues("Set-Cookie", out var s))
                {
                    _cookies.SetCookies(message.RequestUri, string.Join(", ", s));
                }

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
    }

    public class BotHttpHeaders : HttpHeaders
    {
    }
}
