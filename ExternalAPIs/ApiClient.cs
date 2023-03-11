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

        protected virtual Task<HttpResponseMessage> sendAsync(HttpRequestMessage request, bool withToken = true)
        {
            if(withToken)
            {
                var uri = new UriBuilder(request.RequestUri!);
                uri.WithQuery("access_token", oauth!.AccessToken);
                request.RequestUri = uri.Uri;
            }
            return http.SendAsync(request);
        }

        protected virtual Task<HttpResponseMessage> postAsync(string endpoint, Dictionary<string, string>? queryParams = null, bool withToken = true)
        {
            queryParams ??= new();
            var req = new HttpRequestMessage(HttpMethod.Post, baseAddress + queryParams.ToQueryString(endpoint));
            return sendAsync(req, withToken);
        }
        protected virtual Task<HttpResponseMessage> postAsync<TBody>(string endpoint, TBody body, Dictionary<string, string>? queryParams = null, bool withToken = true)
        {
            queryParams ??= new();
            if (withToken && !queryParams.ContainsKey("access_token"))
                queryParams["access_token"] = oauth!.AccessToken!;
            HttpContent? content = null;
            if (body != null)
                content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var req = new HttpRequestMessage(HttpMethod.Post, baseAddress + queryParams.ToQueryString(endpoint));
            req.Content = content;
            return sendAsync(req, withToken);
        }

        protected virtual Task<HttpResponseMessage> getAsync(string endpoint, Dictionary<string, string>? queryParams = null, bool withToken = true)
        {
            queryParams ??= new();
            var req = new HttpRequestMessage(HttpMethod.Get, baseAddress + queryParams.ToQueryString(endpoint));
            return sendAsync(req, withToken);
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
