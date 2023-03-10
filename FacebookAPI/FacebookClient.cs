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
                .WithQuery("redirect_uri", redirect_uri)
                .WithQuery("fields", "expires_in,access_token");
            var response = await http.GetAsync(uri.Uri);
            if (!response.IsSuccessStatusCode) throw await HttpException.FromResponse(response);
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<LongLivedToken>(content);
            oauth = new(result.expires_in.GetValueOrDefault(3600))
            {
                access_token = result.access_token
            };
        }

        public async Task<LongLivedToken> GetLongLivedAccessToken(string app_id, string app_secret, string? access_token = null)
        {
            CheckLogin();
            var response = await getAsync("/oauth/access_token", new()
            {
                { "grant_type", "fb_exchange_token" },
                { "client_secret", app_secret },
                { "client_id", app_id },
                { "fb_exchange_token", access_token ?? oauth!.access_token! },
                { "fields", "expires_in,access_token"}
            }, withToken: false);
            await response.EnsureSuccess();
            var content = await response.Content?.ReadAsStringAsync()!;
            var result = JsonSerializer.Deserialize<LongLivedToken>(content)!;
            result.expires_in ??= 3600 * 24 * 60;
            if (access_token == null)
            { // we updated our own oauth
                oauth!.access_token = result.access_token;
            }
            return result;
        }
        public async Task<FBUser> GetMeAsync()
        {
            CheckLogin();
            var response = await getAsync("/me", new() { { "fields", "id,name" } });
            await response.EnsureSuccess();
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FBUser>(content)!;
        }

        public async Task<IReadOnlyCollection<string>> GetUserMediaAsync(string userId)
        {
            var response = await getAsync($"/{userId}/media");
            await response.EnsureSuccess();
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonObject.Parse(content)!;
            var d = json["data"] as JsonArray;
            return d.Select(x =>
            {
                var child = x as JsonObject;
                var id = child["id"];
                return id.ToString();
            }).ToArray();
        }
        public async Task<Instagram.IGMedia> GetMediaAsync(string mediaId, Instagram.IGMediaFields fields)
        {
            var response = await getAsync($"/{mediaId}", new() { { "fields", string.Join(',', fields.ToFlagList())} });
            await response.EnsureSuccess();
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<Instagram.IGMedia>(content)!;
        }

        public async Task<IReadOnlyCollection<FBPage>> GetMyAccountsAsync()
        {
            var response = await getAsync("/me/accounts");
            await response.EnsureSuccess();
            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<FBPageList>(content)!;
            return data.Data;
        }

        public async Task<string?> GetPageInstagramAccountAsync(string pageId)
        {
            var response = await getAsync($"/{pageId}", new() { { "fields", "instagram_business_account" } });
            await response.EnsureSuccess();
            var content = await response.Content.ReadAsStringAsync();
            var j = JsonObject.Parse(content);
            var obj = j["instagram_business_account"] as JsonObject;
            return obj?["id"]?.ToString() ?? null;
        }

        #region Instagram Media Publishing
        public async Task<string> CreateIGMediaContainer(string userId, string image_url, string caption, string[] userTags = null)
        {
            var query = new Dictionary<string, string>();
            query["image_url"] = image_url;
            query["caption"] = caption;
            if(userTags != null)
            {
                var tags = new JsonArray();
                foreach(var username in tags)
                {
                    var obj = new JsonObject();
                    obj["username"] = username;
                    obj["x"] = 0.0;
                    obj["y"] = 0.0;
                    tags.Add(obj);
                }
                query["userTags"] = tags.ToString(); 
            }
            var response = await postAsync($"/{userId}/media", query);
            await response.EnsureSuccess();
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonObject.Parse(content) as JsonObject;
            return json["id"].ToString();
        }
        public async Task<string> PublishIGMediaContainer(string userId, string containerId)
        {
            var q = new Dictionary<string, string>()
            {
                { "creation_id", containerId }
            };
            var response = await postAsync($"/{userId}/media_publish", q);
            await response.EnsureSuccess();
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonObject.Parse(content) as JsonObject;
            return json["id"].ToString();
        }


        #endregion

        Task<HttpResponseMessage> postAsync(string endpoint, Dictionary<string, string> queryParams, bool withToken = true)
        {
            if (!queryParams.ContainsKey("access_token") && withToken)
                queryParams["access_token"] = oauth!.access_token!;
            return http.PostAsync(baseAddress + queryParams.ToQueryString(endpoint), null);
        }


        Task<HttpResponseMessage> getAsync(string endpoint, Dictionary<string, string>? queryParams = null, bool withToken = true)
        {
            queryParams ??= new();
            if (!queryParams.ContainsKey("access_token") && withToken)
                queryParams["access_token"] = oauth!.access_token!;
            return http.GetAsync(baseAddress + queryParams.ToQueryString(endpoint));
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
