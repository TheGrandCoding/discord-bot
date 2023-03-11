using ExternalAPIs.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExternalAPIs
{
    public class TikTokClient : ApiClient<OAuthToken>
    {
        class TiktokOAuthResponse : OAuthResponse
        {
            public string open_id { get; set; }
            public string scope { get; set; }
            public string refresh_token { get; set; }
            public int refresh_expires_in { get; set; }
        }
        [Flags]
        public enum TikTokAuthScopes
        {
            UserInfoBasic   = 0b001,
            VideoList       = 0b010,
            VideoUpload     = 0b100,

            All = UserInfoBasic | VideoList | VideoUpload
        }
        public TikTokClient(HttpClient http) : base("https://open.tiktokapis.com", http)
        {
        }

        public static string GetRedirectUri(string client_key, TikTokAuthScopes scopes, string redirect_uri, string state)
        {
            var f = string.Join(',', scopes.ToDotList());
            var uri = new UriBuilder("https://www.tiktok.com/auth/authorize/")
                .WithQuery("client_key", client_key)
                .WithQuery("scope", f)
                .WithQuery("redirect_uri", redirect_uri)
                .WithQuery("state", state)
                .WithQuery("response_type", "code");
            return uri.ToString();
        }

        async Task<TikTokOAuthToken> exchangeCode(string client_key, string client_secret, string code)
        {
            var uri = new UriBuilder("https://open-api.tiktok.com/oauth/access_token/")
                .WithQuery("client_key", client_key)
                .WithQuery("client_secret", client_secret)
                .WithQuery("code", code)
                .WithQuery("grant_type", "authorization_code");
            var response = await http.PostAsync(uri.Uri, null);
            await response.EnsureSuccess();
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TikTokOAuthToken>(content)!;
        }







        protected override Task<HttpResponseMessage> sendAsync(HttpRequestMessage request, bool withToken = true)
        {
            if(withToken)
                request.Headers.Add("Authorization", "Bearer " + oauth!.AccessToken);
            return base.sendAsync(request, false);
        }
    }
}
