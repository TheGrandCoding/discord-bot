using ExternalAPIs.Helpers;
using ExternalAPIs.TikTok;
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
    public class TikTokClient : ApiClient<TikTokOAuthToken>
    {
        class TiktokOAuthResponse : OAuthResponse
        {
            public string open_id { get; set; }
            public string scope { get; set; }
            public string refresh_token { get; set; }
            public int refresh_expires_in { get; set; }
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

        public static async Task<TikTokClient> CreateOAuthAsync(string oauthCode, string client_key, string client_secret, HttpClient http)
        {
            var client = new TikTokClient(http);
            client.oauth = await client.exchangeCode(client_key, client_secret, oauthCode);
            return client;
        }
        public static TikTokClient Create(string access_token, DateTime expires_at, string refresh_token, DateTime refresh_expires_at, HttpClient http)
        {
            var client = new TikTokClient(http);
            client.oauth = new(access_token, expires_at, refresh_token, refresh_expires_at);
            return client;
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
            var obj = JsonNode.Parse(content) as JsonObject;
            var message = obj["message"].ToString();
            if(message == "success")
                return JsonSerializer.Deserialize<TikTokOAuthToken>(obj["data"], new JsonSerializerOptions()
                {
                    IncludeFields = true
                })!;
            throw new HttpException(null, content);
        }


        public async Task<TikTokUser> GetMeAsync(TikTokUserFields fields)
        {
            CheckLogin();
            var f = fields.ToSnakeCaseList();
            var response = await getAsync("/v2/user/info/", new() { { "fields", String.Join(',', f) } });
            await response.EnsureSuccess();
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TikTokUser>(content)!;
        }
        class videoFetch
        {
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public ulong? cursor { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? max_count { get; set; }
        }

        public async IAsyncEnumerable<TikTokVideo> GetMyVideosAsync(TikTokVideoFields field)
        {
            var f = field.ToSnakeCaseList();
            var query = new Dictionary<string, string>() { { "fields", string.Join(',', f) } };
            bool has_more = true;
            var fetchData = new videoFetch();
            fetchData.max_count = 10;
            while (has_more)
            {
                HttpResponseMessage response;
                if(fetchData.cursor.HasValue || fetchData.max_count.HasValue)
                    response = await postAsync<videoFetch>("/v2/video/list/", fetchData,  query);
                else
                    response = await postAsync("/v2/video/list/", queryParams: query);
                await response.EnsureSuccess();
                var content = await response.Content.ReadAsStringAsync();
                var parsed = JsonSerializer.Deserialize<TikTokAPIResponse<TikTokVideoList>>(content);
                foreach (var video in parsed.data.videos)
                {
                    yield return video;
                }
                has_more = parsed.data.has_more;
                fetchData.cursor = parsed.data.cursor;
            }
        }

        class videoQueryFilters
        {
            public string[] video_ids { get; set; }
        }
        class videoQuery
        {
            public videoQueryFilters filters { get; set; }
        }
        public async Task<TikTokVideo[]> GetMyVideoAsync(string[] ids, TikTokVideoFields fields)
        {
            var body = new videoQuery()
            {
                filters = new() { video_ids = ids },
            };
            var response = await postAsync("/v2/video/query/", body, new() { { "fields", string.Join(',', fields.ToSnakeCaseList())} });
            await response.EnsureSuccess();
            var content = await response.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<TikTokAPIResponse<TikTokVideoList>>(content);
            return parsed.data.videos;
        }


        protected override Task<HttpResponseMessage> sendAsync(HttpRequestMessage request, bool withToken = true)
        {
            if(withToken)
                request.Headers.Add("Authorization", "Bearer " + oauth!.AccessToken);
            return base.sendAsync(request, false);
        }
    }
}
