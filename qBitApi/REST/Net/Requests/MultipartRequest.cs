using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace qBitApi.REST.Net.Requests
{
    internal class MultipartRestRequest : RestRequest
    {
        public IReadOnlyDictionary<string, object> MultipartParams { get; }

        public MultipartRestRequest(RestClient client, string method, string endpoint, IReadOnlyDictionary<string, object> multipartParams, RequestOptions options)
            : base(client, method, endpoint, options)
        {
            MultipartParams = multipartParams;
        }

        public override async Task<RestResponse> SendAsync()
        {
            return await Client.SendAsync(Method, Endpoint, MultipartParams, Options.CancelToken, Options.HeaderOnly, Options.AuditLogReason).ConfigureAwait(false);
        }
    }
}
