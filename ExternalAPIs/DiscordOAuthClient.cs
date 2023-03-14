using ExternalAPIs.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ExternalAPIs
{
    [Flags]
    public enum DiscordOAuthScopes
    {
        Bot                 = 0b000001,
        Email               = 0b000010,
        GuildsJoin          = 0b000100,
        GuildsMembersRead   = 0b001000,
        Identify            = 0b010000,
        WebhookIncoming     = 0b100000
    }
    public class DiscordWebhook
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }
        [JsonPropertyName("token")]
        public string Token { get; set; }
        [JsonPropertyName("type")]
        public int Type { get; set; }
        [JsonPropertyName("guild_id")]
        public string GuildId { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
    public class DiscordOAuthToken : OAuthToken
    {
        public DiscordOAuthToken(string access_token, DateTime expires_at, string? token_type = null) : base(access_token, expires_at, token_type)
        {
        }
        public DiscordOAuthToken(string access_token, int expires_in, string? token_type = null) : base(access_token, expires_in, token_type)
        {
        }
        [JsonConstructor]
        public DiscordOAuthToken() : base(null, 0, null)
        {

        }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }
        [JsonPropertyName("scope")]
        public string Scope { get; set; }

        [JsonPropertyName("webhook")]
        public DiscordWebhook? Webhook { get; set; }
    }
    public class DiscordOAuthClient : ApiClient<DiscordOAuthToken>
    {
        public DiscordOAuthClient(HttpClient http) : base("https://discord.com/api", http)
        {
        }

        public static Uri GetRedirectUri(string client_id, string redirect_url, DiscordOAuthScopes scopes, string? state = null)
        {
            var sc = scopes.ToDotList();
            var url = new UriBuilder("https://discord.com/api/oauth2/authorize")
                .WithQuery("client_id", client_id)
                .WithQuery("redirect_uri", redirect_url)
                .WithQuery("scope", string.Join(',', sc))
                .WithQuery("response_type", "code");
            if (state != null)
                url.WithQuery("state", state);
            return url.Uri;
        }


        public static async Task<DiscordOAuthClient> CreateOAuthAsync(string oauthCode, string app_id, string app_secret, DiscordOAuthScopes scopes, string redirect_uri, HttpClient http)
        {
            var client = new DiscordOAuthClient(http);
            var sc = string.Join(',', scopes.ToDotList());
            await client.ExchangeAuthorisationCode(oauthCode, app_id, sc, app_secret, redirect_uri);
            return client;
        }
        public static DiscordOAuthClient Create(string access_token, DateTime expires_at, HttpClient http)
        {
            var client = new DiscordOAuthClient(http);
            client.oauth = new(access_token, expires_at, null);
            return client;
        }

        async Task ExchangeAuthorisationCode(string oauthcode, string app_id, string scopes, string app_secret, string redirect_uri)
        {
            var data = new Dictionary<string, string>();
            data.Add("client_id", app_id);
            data.Add("client_secret", app_secret);
            data.Add("code", oauthcode);
            data.Add("grant_type", "authorization_code");
            data.Add("redirect_uri", redirect_uri);
            data.Add("scopes", scopes);
            var response = await http.PostAsync("https://discord.com/api/oauth2/token", new FormUrlEncodedContent(data));
            if (!response.IsSuccessStatusCode) throw await HttpException.FromResponse(response);
            var content = await response.Content.ReadAsStringAsync();
            oauth = JsonSerializer.Deserialize<DiscordOAuthToken>(content)!;
        }

        protected override Task<HttpResponseMessage> sendAsync(HttpRequestMessage request, bool withToken = true)
        {
            if(withToken)
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", oauth.AccessToken);
            return base.sendAsync(request, false);
        }

        public async Task<JsonObject> GetUserInformation()
        {
            CheckLogin();
            if (oauth.Scope.Contains("identify") == false)
                throw new InvalidOperationException("'identify' was not among the scopes requested.");
            var response = await getAsync("/users/@me");
            await response.EnsureSuccess();
            var content = await response.Content.ReadAsStringAsync();
            return JsonNode.Parse(content) as JsonObject;
        }
        public async Task<HttpResponseMessage> JoinToServer(ulong guildId, ulong userId, string botToken)
        {
            CheckLogin();
            if (oauth.Scope.Contains("guilds.join") == false)
                throw new InvalidOperationException("'guilds.join' was not among the scopes requested.");
            var request = new HttpRequestMessage(HttpMethod.Put, baseAddress + $"/guilds/{guildId}/members/{userId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", botToken);
            var jobj = new JsonObject();
            jobj["access_token"] = oauth.AccessToken;
            request.Content = new StringContent(jobj.ToString(), Encoding.UTF8, "application/json");
            var response = await sendAsync(request, false);
            await response.EnsureSuccess();
            return response;
        }
    }
}
