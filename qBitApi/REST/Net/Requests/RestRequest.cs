using qBitApi.REST.Net.Requests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace qBitApi.REST.Net
{
    public class RestRequest
    {
        internal RestClient Client { get; }
        public string Method { get; }
        public string Endpoint { get; }
        public DateTimeOffset? TimeoutAt { get; }
        public TaskCompletionSource<Stream> Promise { get; }
        public RequestOptions Options { get; }

        internal RestRequest(RestClient client, string method, string endpoint, RequestOptions options)
        {
            options ??= RequestOptions.Default;

            Client = client;
            Method = method;
            Endpoint = endpoint;
            Options = options;
            TimeoutAt = options.Timeout.HasValue ? DateTimeOffset.UtcNow.AddMilliseconds(options.Timeout.Value) : (DateTimeOffset?)null;
            Promise = new TaskCompletionSource<Stream>();
        }

        public virtual async Task<RestResponse> SendAsync()
        {
            return await Client.SendAsync(Method, Endpoint, Options.CancelToken, Options.HeaderOnly, Options.AuditLogReason).ConfigureAwait(false);
        }
    }
}
