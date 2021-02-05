using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace qBitApi.REST.Net.Requests
{
    internal class FormRestRequest : RestRequest
    {
        public IReadOnlyDictionary<string, object> Params { get; }

        public FormRestRequest(RestClient client, string method, string endpoint, IReadOnlyDictionary<string, object> formParams, RequestOptions options)
            : base(client, method, endpoint, options)
        {
            Params = formParams;
        }

        public override async Task<RestResponse> SendAsync()
        {
            return await Client.SendFormAsync(Method, Endpoint, Params, Options.CancelToken, Options.HeaderOnly).ConfigureAwait(false);
        }
    }
}
