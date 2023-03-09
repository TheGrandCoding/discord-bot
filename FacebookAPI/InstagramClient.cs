using FacebookAPI.Helpers;
using FacebookAPI.Instagram;
using System.Text.Json;

namespace FacebookAPI
{
    public class OAuthResponse
    {
        public ulong user_id { get; set; }
        public string access_token { get; set; }
    }
    public class InstagramClient
    {
        private HttpClient http;
        public OAuthResponse? oauth;
        private string baseAddress = "https://graph.instagram.com";
        internal InstagramClient(HttpClient httpClient)
        {
            http = httpClient;
        }
        static List<string> convertFlags<T>(T value) where T: struct, Enum 
        {
            var ls = new List<string>();
            foreach(var name in Enum.GetNames<T>())
            {
                if (name == "All") continue;
                T _flagV = (T)Enum.Parse(typeof(T), name);
                if(value.HasFlag(_flagV))
                {
                    ls.Add(name.ToSnakeCase());
                }
            }
            return ls;
        }

        public static Uri GetBasicRedirectUri(string client_id, string redirect_url, Instagram.BasicAPIScopes scopes, string? state = null)
        {
            var sc = convertFlags(scopes | BasicAPIScopes.UserProfile);
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
        public static InstagramClient Create(string access_token, string user_id, HttpClient http)
        {
            if (!ulong.TryParse(user_id, out var id)) throw new ArgumentException("Must be ulong", nameof(user_id));
            var client = new InstagramClient(http);
            client.oauth = new()
            {
                access_token = access_token,
                user_id = id
            };
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
            oauth = JsonSerializer.Deserialize<OAuthResponse>(content)!;
        }

        public async Task<IGUser> GetMeAsync(IGUserFields fields)
        {
            CheckLogin();
            var f = convertFlags(fields);
            var response = await getAsync("/me?fields=" + Uri.EscapeDataString(String.Join(",", f)));
            if(!response.IsSuccessStatusCode) throw await HttpException.FromResponse(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IGUser>(content)!;
        }

        public async Task<IGMedia> GetMediaAsync(string mediaId, IGMediaFields fields)
        {
            CheckLogin();
            var f = convertFlags(fields);
            var response = await getAsync($"/{mediaId}?fields=" + Uri.EscapeDataString(String.Join(",", f)));
            if (!response.IsSuccessStatusCode) throw await HttpException.FromResponse(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IGMedia>(content)!;
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
        }
    }
}