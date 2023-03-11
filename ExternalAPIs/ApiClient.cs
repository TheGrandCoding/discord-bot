using ExternalAPIs.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExternalAPIs
{
    public class ApiClient<TToken> where TToken : OAuthToken
    {
        public TToken? oauth;
        protected HttpClient http;
        protected string baseAddress;
        public ApiClient(string baseAddress, HttpClient http)
        {
            this.baseAddress = baseAddress;
            this.http = http;
        }

        protected virtual Task<HttpResponseMessage> postWithQueryAsync(string endpoint, Dictionary<string, string> queryParams, bool withToken = true)
        {
            if (!queryParams.ContainsKey("access_token") && withToken)
                queryParams["access_token"] = oauth!.AccessToken!;
            return http.PostAsync(baseAddress + queryParams.ToQueryString(endpoint), null);
        }

        protected virtual Task<HttpResponseMessage> getAsync(string endpoint, Dictionary<string, string>? queryParams = null, bool withToken = true)
        {
            queryParams ??= new();
            if (!queryParams.ContainsKey("access_token") && withToken)
                queryParams["access_token"] = oauth!.AccessToken!;
            return http.GetAsync(baseAddress + queryParams.ToQueryString(endpoint));
        }

        [System.Diagnostics.DebuggerStepThrough]
        protected void CheckLogin()
        {
            if (oauth == null || string.IsNullOrWhiteSpace(oauth.AccessToken))
                throw TokenException.ForInvalid();
            if (oauth.ExpiresAt.GetValueOrDefault(DateTime.MinValue) < DateTime.Now)
                throw TokenException.ForExpired();
        }
    }
}
