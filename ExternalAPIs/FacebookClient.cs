using ExternalAPIs.Facebook;
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
    public class FacebookClient : ApiClient<OAuthToken>
    {
        internal FacebookClient(HttpClient httpClient) : base("https://graph.facebook.com/v16.0", httpClient)
        {
        }

        public static Uri GetRedirectUri(string client_id, string redirect_url, Facebook.OAuthScopes scopes, string? state = null)
        {
            var sc = (scopes | Facebook.OAuthScopes.PublicProfile).ToSnakeCaseList();
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
            var result = JsonSerializer.Deserialize<OAuthResponse>(content)!;
            oauth = new(result.AccessToken!, result.expires_in ?? 3600, result.TokenType);
        }

        public async Task<OAuthToken> GetLongLivedAccessToken(string app_id, string app_secret, string? access_token = null)
        {
            CheckLogin();
            var response = await getAsync("/oauth/access_token", new()
            {
                { "grant_type", "fb_exchange_token" },
                { "client_secret", app_secret },
                { "client_id", app_id },
                { "fb_exchange_token", access_token ?? oauth!.AccessToken! },
                { "fields", "expires_in,access_token"}
            }, withToken: false);
            await response.EnsureSuccess();
            var content = await response.Content?.ReadAsStringAsync()!;
            var result = JsonSerializer.Deserialize<OAuthResponse>(content)!;
            var token = new OAuthToken(result.AccessToken!, result.expires_in ?? 3600 * 24 * 60, result.TokenType);
            if (access_token == null)
            { // we updated our own oauth
                oauth = token;
            }
            return token;
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
            var response = await getAsync($"/{mediaId}", new() { { "fields", string.Join(',', fields.ToSnakeCaseList())} });
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


        async Task<string> createContainer(string userId, Dictionary<string, string> query, string caption, bool isCarouselItem, string[] userTags)
        {
            if(!string.IsNullOrEmpty(caption))
                query["caption"] = caption;
            if (userTags != null)
            {
                var tags = new JsonArray();
                foreach (var username in tags)
                {
                    var obj = new JsonObject();
                    obj["username"] = username;
                    obj["x"] = 0.0;
                    obj["y"] = 0.0;
                    tags.Add(obj);
                }
                query["userTags"] = tags.ToString();
            }
            if (isCarouselItem)
                query["is_carousel_item"] = "true";
            var response = await postAsync($"/{userId}/media", queryParams: query);
            await response.EnsureSuccess();
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonObject.Parse(content) as JsonObject;
            return json["id"].ToString();
        }

        public Task<string> CreateIGVideoContainer(string userId, string video_url, string caption, bool isCarouselItem, string[] userTags = null)
        {
            var query = new Dictionary<string, string>();
            query["video_url"] = video_url;
            query["media_type"] = "VIDEO";
            return createContainer(userId, query, caption, isCarouselItem, userTags);
        }
        public Task<string> CreateIGImageContainer(string userId, string image_url, string caption, bool isCarouselItem, string[] userTags = null)
        {
            var query = new Dictionary<string, string>();
            query["image_url"] = image_url;
            return createContainer(userId, query, caption, isCarouselItem, userTags);
        }
        public Task<string> CreateIGCarouselContainer(string userId, string[] childIds, string caption, string[] userTags = null)
        {
            var query = new Dictionary<string, string>();
            query["children"] = string.Join(',', childIds);
            query["media_type"] = "CAROUSEL";
            return createContainer(userId, query, caption, false, userTags);
        }

        public async Task<string> PublishIGMediaContainer(string userId, string containerId)
        {
            var q = new Dictionary<string, string>()
            {
                { "creation_id", containerId }
            };
            var response = await postAsync($"/{userId}/media_publish", queryParams: q);
            await response.EnsureSuccess();
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonObject.Parse(content) as JsonObject;
            return json["id"].ToString();
        }


        #endregion


    }
}
