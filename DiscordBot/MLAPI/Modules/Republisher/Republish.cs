using DiscordBot.Classes;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.Republisher
{

    public class Republish : APIBase
    {
        public Republish(APIContext c) : base(c, "republisher") 
        {
        }

        string getUriBase()
        {
#if DEBUG
            return "https://ngrok uri here";
#else
            return Handler.LocalAPIUrl;
#endif
        }
        async Task<HttpResponseMessage> getAsync(string endpoint)
        {
            var http = Context.Services.GetRequiredService<BotHttpClient>().Child("insta", debug: true);
            char c = endpoint.IndexOf('?') >= 0 ? '&' : '?';
            var response = await http.GetAsync($"https://graph.instagram.com/v16.0{endpoint}{c}access_token={Uri.EscapeDataString(Context.User.Instagram.AccessToken)}");
            return response;
        }

        class InstagramPagerCursors
        {
            public string Before { get; set; }
            public string After { get; set; }
        }
        class InstagramPager
        {
            public InstagramPagerCursors Cursors { get; set; }
        }
        class InstagramMedia
        {
            public string Id { get; set; }
            public string Caption { get; set; }
            [JsonProperty("media_type")]
            public string MediaType { get; set; }
            [JsonProperty("media_url")]
            public string MediaUrl { get; set; }
            public string Permalink { get; set; }
            public DateTime Timestamp { get; set; }
            public string Username { get; set; }

            public Div ToHtml()
            {
                var div = new Div(id: $"ig_{Id}", cls: "ig_media");
                var img = new Img(MediaUrl)
                {
                    Style = "width: 32px"
                };
                div.Children.Add(img);
                var anchor = new Anchor(Permalink, Caption);
                div.Children.Add(anchor);
                return div;
            }
        }
        class InstagramMediaPager
        {
            public InstagramMedia[] Data { get; set; }
            public InstagramMediaPager Paging { get; set; }
        }
        class InstagramUser
        {
            public string Id { get; set; }
            public string Username { get; set; }
            [JsonProperty("account_type")]
            public string AccountType { get; set; }
            [JsonProperty("media_count")]
            public int MediaCount { get; set; }
            public InstagramMediaPager Media { get; set; }
        }



        [Method("GET"), Path("/republisher")]
        public async Task ViewInstaItems()
        {
            if(Context.User?.Instagram?.IsInvalid() ?? true)
            {
                var url = new UrlBuilder("https://api.instagram.com/oauth/authorize");
                url.Add("client_id", Program.Configuration["tokens:instagram:app_id"]);
                url.Add("redirect_uri", $"{getUriBase()}/oauth/instagram");
                url.Add("scope", "user_profile,user_media");
                url.Add("response_type", "code");
                url.Add("state", "123");
                await RespondRedirect(url);
                return;
            }
            var resp = await getAsync($"/me?fields=id,username,account_type,media_count,media");
            var content = await resp.Content.ReadAsStringAsync();
            if(resp.IsSuccessStatusCode)
            {
                var ig = "";
                var j = JObject.Parse(content);
                var arr = new JArray();
                var user = j.ToObject<InstagramUser>();
                foreach(var media in user.Media.Data)
                {
                    var full = await getAsync($"/{media.Id}?fields=caption,id,media_type,media_url,permalink,thumbnail_url,timestamp,username");
                    var body = await full.Content.ReadAsStringAsync();
                    var fullMedia = JsonConvert.DeserializeObject<InstagramMedia>(body);
                    arr.Add(JObject.Parse(body));
                    ig += fullMedia.ToHtml() + "\n";
                }
                j["_full"] = arr;
                await ReplyFile("select.html", 200, new Replacements()
                    .Add("instagramPosts", ig)
                    .Add("rawcode", j.ToString(Formatting.Indented)));
            } else
            {
                await RespondRaw($"Error {resp.StatusCode} {resp.ReasonPhrase}: {content}");
            }
        }
        [Method("GET"), Path("/oauth/instagram")]
        public async Task HandleOauth(string code = null, string state = null, 
                                      string error = null, string error_reason = null, string error_description = null)
        {
            if(error != null)
            {
                await RespondRaw($"Error: {error}, {error_reason}: {error_description}");
                return;
            }

            var data = new Dictionary<string, string>();
            data.Add("client_id", Program.Configuration["tokens:instagram:app_id"]);
            data.Add("client_secret", Program.Configuration["tokens:instagram:app_secret"]);
            data.Add("code", code);
            data.Add("grant_type", "authorization_code");
            data.Add("redirect_uri", $"{getUriBase}/oauth/instagram");
            var http = Context.Services.GetRequiredService<BotHttpClient>().Child("insta", debug: true);
            var response = await http.PostAsync("https://api.instagram.com/oauth/access_token", new FormUrlEncodedContent(data));
            var content = await response.Content.ReadAsStringAsync();
            var shortResponse = JObject.Parse(content);
            string id;
            if(shortResponse.TryGetValue("user_id", out var token))
            {
                id = token.ToObject<string>();
            } else
            {
                RespondRaw(shortResponse.ToString(Formatting.Indented), 400);
                return;
            }
            if(Context.User == null)
            {
                Context.User = await Context.BotDB.GetUserByInstagram(id, true);
                await Handler.SetNewLoginSession(Context, Context.User, true, true);
            }
            Context.User.Instagram = new BotDbInstagram()
            {
                AccountId = id,
                AccessToken = shortResponse["access_token"].ToObject<string>(),
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            await RespondRedirect("/republisher");
        }
    }
}
