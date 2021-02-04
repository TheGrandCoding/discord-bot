﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace qBitApi.REST.Net.Requests
{
    internal class JsonRestRequest : RestRequest
    {
        public string Json { get; }

        public JsonRestRequest(RestClient client, string method, string endpoint, string json, RequestOptions options)
            : base(client, method, endpoint, options)
        {
            Json = json;
        }

        public override async Task<RestResponse> SendAsync()
        {
            return await Client.SendAsync(Method, Endpoint, Json, Options.CancelToken, Options.HeaderOnly, Options.AuditLogReason).ConfigureAwait(false);
        }
    }
}
