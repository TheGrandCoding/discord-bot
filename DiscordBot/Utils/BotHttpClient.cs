using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Utils
{
    public class BotHttpClient : HttpClient
    {
        public new async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption,
            CancellationToken cancellationToken)
        {
            var source = "unknown:unknown#0";
            var stack = new StackTrace(true);
            foreach(var x in stack.GetFrames())
            {
                var method = x.GetMethod();
                if (method == null)
                    continue;
                if (method.DeclaringType == typeof(BotHttpClient) || method.DeclaringType == typeof(HttpClient))
                    continue;
                source = $"{method.DeclaringType.Name}:{method.Name}#{x.GetFileLineNumber()}";
            }
            var sending = $"{request.Method} {request.RequestUri}";
            Program.LogVerbose(sending, $"{source}-Send");
            var ms = new Stopwatch();
            ms.Start();
            var response = await base.SendAsync(request, completionOption, cancellationToken);
            ms.Stop();
            Program.LogVerbose($"{response.StatusCode} {sending}", $"{source}-{response.StatusCode}");
            return response;
        }
    }
}
