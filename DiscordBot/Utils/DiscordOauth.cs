using Discord;
using DiscordBot.MLAPI;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Utils
{
    public class DiscordOauth
    {
        public string Scope { get; }
        public string AccessToken { get; private set; }
        public IUser UserInfo { get; private set; }
        Classes.BotHttpClient client;

        class JsonUser : IUser
        {
            [JsonProperty("avatar")]
            public string AvatarId { get; set; }
            public string Discriminator { get; set; }
            public ushort DiscriminatorValue => ushort.Parse(Discriminator);

            public bool IsBot { get; set; }

            public bool IsWebhook { get; set; }

            public string Username { get; set; }

            public DateTimeOffset CreatedAt { get; set; }

            public ulong Id { get; set; }

            public string Mention => $"<@{Id}>";

            public IActivity Activity { get; set; }

            public UserStatus Status { get; set; }

            public IImmutableSet<ClientType> ActiveClients { get; set; }
            public IImmutableList<IActivity> Activities { get; set; }
            public UserProperties? PublicFlags { get; set; }

            public string BannerId => throw new NotImplementedException();

            public Color? AccentColor => throw new NotImplementedException();

            IReadOnlyCollection<ClientType> IPresence.ActiveClients => throw new NotImplementedException();

            IReadOnlyCollection<IActivity> IPresence.Activities => throw new NotImplementedException();

            public Task<IDMChannel> CreateDMChannelAsync(RequestOptions options = null)
            {
                throw new NotImplementedException();
            }

            public string GetAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
                => Discord.CDN.GetUserAvatarUrl(Id, AvatarId, size, format);

            public string GetBannerUrl(ImageFormat format = ImageFormat.Auto, ushort size = 256)
            {
                throw new NotImplementedException();
            }

            public string GetDefaultAvatarUrl()
                => Discord.CDN.GetDefaultUserAvatarUrl(DiscriminatorValue);
            public Task<IDMChannel> GetOrCreateDMChannelAsync(RequestOptions options = null)
            {
                throw new NotImplementedException();
            }
        }

        const string baseUrl = "https://discord.com/api";
        public DiscordOauth(string scope, string code = null)
        {
            Scope = scope;
            client = Program.Services.GetRequiredService<Classes.BotHttpClient>();
            if (code != null)
                SetToken(code).Wait();
        }

        public UrlBuilder GetAuthorisationUrl(string state)
        {
            return UrlBuilder.Discord()
                .Add("response_type", "code")
                .Add("scope", Scope)
                .Add("redirect_uri", Handler.LocalAPIUrl + "/oauth2/discord")
                .Add("state", state);
        }

        async Task<HttpResponseMessage> postJson(JObject json, string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + url);
            request.Headers.Add("User-Agent", "tgc-bot-manual");
            string text = "";
            foreach (JProperty item in json.Children())
                text += $"{item.Name}={System.Web.HttpUtility.UrlEncode(item.Value.ToString())}&";
            text = text.Substring(0, text.Length - 1);
            request.Content = new StringContent(text, Encoding.UTF8, "application/x-www-form-urlencoded");
            return await client.SendAsync(request);
        }

        public async Task SetToken(string code)
        {
            var getToken = new JObject();
            getToken["client_id"] = Program.AppInfo.Id;
            getToken["client_secret"] = Program.Configuration["tokens:appSecret"];
            getToken["grant_type"] = "authorization_code";
            getToken["code"] = code;
            getToken["redirect_uri"] = $"{Handler.LocalAPIUrl}/oauth2/discord";
            getToken["scope"] = Scope;
            var response = await postJson(getToken, "/oauth2/token");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception(content);
            AccessToken = JObject.Parse(content)["access_token"].ToObject<string>();
        }

        public async Task<IUser> GetUserInformation()
        {
            if (!Scope.Contains("identify"))
                throw new InvalidOperationException("'identify' was not among the scopes requested.");
            if (AccessToken == null)
                throw new InvalidOperationException("Access token has not yet been negotiated");
            var request = new HttpRequestMessage(HttpMethod.Get, baseUrl + "/users/@me");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception(content);
            UserInfo = JsonConvert.DeserializeObject<JsonUser>(content);
            return UserInfo;
        }

        public async Task<HttpResponseMessage> JoinToServer(IGuild guild, Classes.BotDbUser user = null)
        {
            if (!Scope.Contains("guilds.join"))
                throw new InvalidOperationException("Joining guilds was not among the scopes requested.");
            if (AccessToken == null)
                throw new Exception("Access token has not yet been negotiated");
            if (user == null && UserInfo != null)
                throw new InvalidOperationException("You must provide a user instance, or call GetUserInformation before.");
            ulong id = user?.Id ?? UserInfo.Id;
            var request = new HttpRequestMessage(HttpMethod.Put, baseUrl + $"/guilds/{guild.Id}/members/{id}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", Program.Configuration["tokens:discord"]);
            var jobj = new JObject();
            jobj["access_token"] = AccessToken;
            request.Content = new StringContent(jobj.ToString(), Encoding.UTF8, "application/json");
            return await client.SendAsync(request);
        }

    }
}
