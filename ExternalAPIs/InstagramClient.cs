using ExternalAPIs.Helpers;
using ExternalAPIs.Instagram;
using System.Text.Json;

namespace ExternalAPIs
{
    public partial class InstagramClient : ApiClient<IGOAuthToken>
    {
        private class IGOAuthResponse : OAuthResponse
        {
            public ulong user_id { get; set; }
        }
        internal InstagramClient(HttpClient httpClient) : base("https://graph.instagram.com", httpClient)
        {
        }
        public static Uri GetBasicRedirectUri(string client_id, string redirect_url, Instagram.BasicAPIScopes scopes, string? state = null)
        {
            var sc = (scopes | BasicAPIScopes.UserProfile).ToFlagList();
            var url = new UriBuilder("https://api.instagram.com/oauth/authorize")
                .WithQuery("client_id", client_id)
                .WithQuery("redirect_uri", redirect_url)
                .WithQuery("scope", string.Join(',', sc))
                .WithQuery("response_type", "code");
            if (state != null)
                url.WithQuery("state", state);
            return url.Uri;
        }
        public static async Task<InstagramClient> CreateOAuthAsync(string oauthCode, string app_id, string app_secret, string redirect_uri, HttpClient http)
        {
            var client = new InstagramClient(http);
            await client.ExchangeAuthorisationCode(oauthCode, app_id, app_secret, redirect_uri);
            return client;
        }
        public static InstagramClient Create(string access_token, string user_id, DateTime expires_at, HttpClient http)
        {
            if (!ulong.TryParse(user_id, out var id)) throw new ArgumentException("Must be ulong", nameof(user_id));
            var client = new InstagramClient(http);
            client.oauth = new(access_token, expires_at, null) { UserId = ulong.Parse(user_id) };
            return client;
        }

        async Task ExchangeAuthorisationCode(string oauthcode, string app_id, string app_secret, string redirect_uri)
        {
            var data = new Dictionary<string, string>();
            data.Add("client_id", app_id);
            data.Add("client_secret", app_secret);
            data.Add("code", oauthcode);
            data.Add("grant_type", "authorization_code");
            data.Add("redirect_uri", redirect_uri);
            var response = await http.PostAsync("https://api.instagram.com/oauth/access_token", new FormUrlEncodedContent(data));
            if(!response.IsSuccessStatusCode) throw await HttpException.FromResponse(response);
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<IGOAuthResponse>(content)!;
            oauth = new(result.AccessToken!, 3600, null) { UserId = result.user_id };
        }
        public async Task<OAuthToken> GetLongLivedAccessToken(string app_secret, string? access_token = null)
        {
            CheckLogin();
            var response = await getAsync("/access_token", new()
            {
                { "grant_type", "ig_exchange_token" },
                { "client_secret", app_secret },
                { "access_token", access_token ?? oauth!.AccessToken }
            });
            await response.EnsureSuccess();
            var content = await response.Content?.ReadAsStringAsync()!;
            var result = JsonSerializer.Deserialize<OAuthResponse>(content)!;
            var token = new IGOAuthToken(result.AccessToken!, result.expires_in ?? 3600 * 24 * 60, result.TokenType);
            if(access_token == null)
            { // we updated our own oauth
                token.UserId = oauth!.UserId;
                oauth = token;
            }
            return token;
        }
        public async Task<OAuthToken> RefreshLongLivedAccessToken(string? access_token = null)
        {
            CheckLogin();
            var response = await getAsync("/access_token", new()
            {
                { "grant_type", "ig_refresh_token" },
                { "access_token", access_token ?? oauth!.AccessToken }
            });
            await response.EnsureSuccess();
            var content = await response.Content?.ReadAsStringAsync()!;
            var result = JsonSerializer.Deserialize<OAuthResponse>(content)!;
            var token = new IGOAuthToken(result.AccessToken!, result.expires_in ?? 3600 * 24 * 60, result.TokenType);
            if (access_token == null)
            { // we updated our own oauth
                token.UserId = oauth!.UserId;
                oauth = token;
            }
            return token;
        }

        public Task<IGUser> GetMeAsync(IGUserFields fields)
            => GetUserAsync("me", fields);
        public async Task<IGUser> GetUserAsync(string userId, IGUserFields fields)
        {
            CheckLogin();
            var f = fields.ToFlagList();
            var response = await getAsync($"/{userId}", new() { 
                { "fields", String.Join(",", f) } 
            });
            if (!response.IsSuccessStatusCode) throw await HttpException.FromResponse(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IGUser>(content)!;
        }

        public async Task<IGMedia> GetMediaAsync(string mediaId, IGMediaFields fields)
        {
            CheckLogin();
            var f = fields.ToFlagList();
            var response = await getAsync($"/{mediaId}", new()
            {
                { "fields", String.Join(",", f) }
            });
            if (!response.IsSuccessStatusCode) throw await HttpException.FromResponse(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IGMedia>(content)!;
        }
    }
}