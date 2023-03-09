using FacebookAPI.Facebook;
using FacebookAPI.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FacebookAPI
{
    public class FBOAuthResponse
    {
        [JsonConstructor]
        public FBOAuthResponse(int expires_in)
        {
            expires_at = DateTime.Now.AddSeconds(expires_in);
        }
        public FBOAuthResponse(string token, DateTime _expires_at)
        {
            access_token = token;
            expires_at = expires_at;
        }
        public string? access_token { get; set; }
        public string? token_type { get; set; }
        public DateTime expires_at { get; }
    }
    public class FacebookClient
    {
        private HttpClient http;
        public FBOAuthResponse? oauth;
        private string baseAddress = "https://graph.facebook.com/v16.0";
        internal FacebookClient(HttpClient httpClient)
        {
            http = httpClient;
        }

        public static Uri GetRedirectUri(string client_id, string redirect_url, Facebook.OAuthScopes scopes, string? state = null)
        {
            var sc = (scopes | Facebook.OAuthScopes.PublicProfile).ToFlagList();
            var url = new UriBuilder("https://www.facebook.com/v16.0/dialog/oauth")
                .WithQuery("client_id", client_id)
                .WithQuery("redirect_uri", redirect_url)
                .WithQuery("scope", string.Join(',', sc))
                .WithQuery("response_type", "code,granted_scopes");
            if (state != null)
                url.WithQuery("state", state);
            return url.Uri;
        }
        public static async Task<FacebookClient> CreateOAuthAsync(string oauthCode, string app_id, string app_secret, string redirect_uri, HttpClient http)
        {
            var client = new FacebookClient(http);
            await client.ExchangeAuthorisationCode(oauthCode, app_id, app_secret, redirect_uri);
            return client;
        }
        public static FacebookClient Create(string access_token, DateTime expires_at, HttpClient http)
        {
            var client = new FacebookClient(http);
            client.oauth = new(access_token, expires_at);
            return client;
        }

        async Task ExchangeAuthorisationCode(string oauthcode, string app_id, string app_secret, string redirect_uri)
        {
            var uri = new UriBuilder($"{baseAddress}/oauth/access_token")
                .WithQuery("client_id", app_id)
                .WithQuery("client_secret", app_secret)
                .WithQuery("code", oauthcode)
                .WithQuery("redirect_uri", redirect_uri);
            var response = await http.GetAsync(uri.Uri);
            if (!response.IsSuccessStatusCode) throw await HttpException.FromResponse(response);
            var content = await response.Content.ReadAsStringAsync();
            var obj = JsonObject.Parse(content)!;
            oauth = new((int)obj["expires_in"]!)
            {
                access_token = obj["access_token"]!.ToString()
            };
        }

        public async Task<FBUser> GetMeAsync()
        {
            var response = await getAsync("/me?fields=" + Uri.EscapeDataString("id,name"));
            if (!response.IsSuccessStatusCode) throw await HttpException.FromResponse(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FBUser>(content)!;
        }

        public async Task<IReadOnlyCollection<FBPage>> GetMyAccountsAsync()
        {
            var response = await getAsync("/me/accounts");
            if (!response.IsSuccessStatusCode) throw await HttpException.FromResponse(response);
            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<FBPageList>(content)!;
            return data.Data;
        }

        public async Task<string?> GetPageInstagramAccountAsync(string pageId)
        {
            var response = await getAsync($"/{pageId}?fields=instagram_business_account");
            if (!response.IsSuccessStatusCode) throw await HttpException.FromResponse(response);
            var content = await response.Content.ReadAsStringAsync();
            var j = JsonObject.Parse(content);
            var obj = j["instagram_business_account"] as JsonObject;
            return obj?["id"]?.ToString() ?? null;
        }


        Task<HttpResponseMessage> getAsync(string endpoint)
        {
            char sep = endpoint.Contains('?') ? '&' : '?';
            return http.GetAsync(baseAddress + endpoint + sep + "access_token=" + oauth!.access_token);
        }

        private void CheckLogin()
        {
            if (oauth == null || string.IsNullOrWhiteSpace(oauth.access_token))
                throw new Exception($"Client was used before it was logged in.");
            if (oauth.expires_at < DateTime.Now)
                throw new Exception("Token has expired");
        }
    }
}
