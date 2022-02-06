using Discord;
using DiscordBot.Classes;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class TraktService : EncryptedService
    {
        public Dictionary<ulong, TraktUserSave> Users { get; set; } = new Dictionary<ulong, TraktUserSave>();
        public Dictionary<int, string> CachedShowNetworks { get; set; } = new Dictionary<int, string>();

        public BotHttpClient HTTP { get; private set; }

        private const string clientIdConfig = "tokens:trakt:client_id";
        private const string clientSecretConfig = "tokens:trakt:client_secret";
        private const string traktApiBase = "https://api.trakt.tv";

        public Uri OAuthRedirectUri
        {
            get
            {
                return new UrlBuilder("https://trakt.tv/oauth/authorize")
                    .Add("response_type", "code")
                    .Add("client_id", Program.Configuration[clientIdConfig])
                    .Add("redirect_uri", OAuthUri);
                    
            }
        }
        public string OAuthUri => DiscordBot.MLAPI.Handler.LocalAPIUrl + "/oauth2/trakt";
        public string ClientId => Program.Configuration[clientIdConfig];

        protected override string KeyLocation => "trakt_service";

        public override string GenerateSave()
        {
            var sv = new TraktSave()
            {
                Users = Users,
                shows = CachedShowNetworks
            };
            return Program.Serialise(sv);
        }

        public override void OnReady()
        {
            EnsureConfiguration(clientIdConfig);
            EnsureConfiguration(clientSecretConfig);
            var sv = Program.Deserialise<TraktSave>(ReadSave());
            Users = sv.Users ?? new Dictionary<ulong, TraktUserSave>();
            CachedShowNetworks = sv.shows ?? new Dictionary<int, string>();
            HTTP = Program.Services.GetRequiredService<BotHttpClient>()
                .Child(Name, debug: Program.BOT_DEBUG);
        }

        public override void OnDailyTick()
        {
            Send().Wait();
        }

        public async Task AddUser(ulong userId, string code)
        {
            var token = await GetToken(code);
            Users[userId] = new TraktUserSave()
            {
                AccessToken = token
            };
            OnSave();
        }

        public async Task<TraktAccessToken> GetToken(string code, string kind = "code")
        {
            var json = new JObject();
            json[kind] = code;
            json["client_id"] = ClientId;
            json["client_secret"] = Program.Configuration[clientSecretConfig];
            json["redirect_uri"] = OAuthUri;
            json["grant_type"] = kind == "code" ? "authorization_code" : "refresh_token";

            var body = new StringContent(json.ToString(), Encoding.UTF8, "application/json");

            var response = await HTTP.PostAsync(traktApiBase + "/oauth/token", body);
            var respBody = await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to exchange OAuth code: {response.StatusCode} {respBody}");

            var respJson = JObject.Parse(respBody);
            return new TraktAccessToken()
            {
                AccessToken = respJson["access_token"].ToObject<string>(),
                RefreshToken = respJson["refresh_token"].ToObject<string>(),
                IssuedAt = DateTime.Now
            };
        }
        public Task<TraktAccessToken> RefreshToken(string refresh_token)
            => GetToken(refresh_token, "refresh_token");
        public async Task<IReadOnlyCollection<TraktAiringInfo>> GetCalendarShowsAsync(string token, DateTime? startDate = null, int days = 1)
        {
            DateTime date = startDate ?? DateTime.Now;
            var request = new HttpRequestMessage(HttpMethod.Get, traktApiBase + $"/calendars/my/shows/{date:yyyy-MM-dd}/{days}");
            request.Headers.Add("Authorization", "Bearer " + token);
            request.Headers.Add("trakt-api-version", "2");
            request.Headers.Add("trakt-api-key", ClientId);

            var response = await HTTP.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to get calendar shows: {body}");

            var ls = JsonConvert.DeserializeObject<List<TraktAiringInfo>>(body);
            return ls.ToImmutableArray();
        }
        public async Task<string> GetShowNetwork(int traktId)
        {
            if (CachedShowNetworks.TryGetValue(traktId, out var s))
                return s;

            var request = new HttpRequestMessage(HttpMethod.Get, TraktService.traktApiBase + $"/shows/{traktId}?extended=full");
            request.Headers.Add("trakt-api-version", "2");
            request.Headers.Add("trakt-api-key", ClientId);

            var response = await HTTP.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to get show info: {body}");
            var json = JObject.Parse(body);
            var network = json["network"].ToObject<string>();
            CachedShowNetworks.Add(traktId, network);
            OnSave();
            return network;
        }

        public async Task Send()
        {
            foreach((var userId, var save) in Users)
            {
                try
                {
                    if (save.Channel == null) continue;
                    var token = await save.AccessToken.GetToken(this);
                    var airingToday = await GetCalendarShowsAsync(token);

                    var airingByShow = new Dictionary<int, List<TraktAiringInfo>>();

                    var builder = new EmbedBuilder();
                    if(airingToday.Count == 0)
                    {
                        builder.Title = "No episodes airing today";
                        builder.Color = Color.Red;
                    }
                    else
                    {
                        builder.Title = $"{airingToday.Count} episode{(airingToday.Count == 1 ? "" : "s")} airing today";
                        builder.Color = Color.Green;
                    }

                    foreach (var airingInfo in airingToday)
                    {
                        airingByShow.AddInner(airingInfo.Show.Ids.TraktId, airingInfo);
                    }
                    foreach((var key, var ls) in airingByShow)
                    {
                        var show = ls.First().Show;
                        var network = await GetShowNetwork(show.Ids.TraktId);

                        var value = new StringBuilder();
                        value.Append($"**{network}**\r\n");
                        foreach(var episode in ls.OrderBy(x => x.FirstAired))
                        {
                            var stamp = TimestampTag.FromDateTime(episode.FirstAired.DateTime, TimestampTagStyles.Relative);
                            var row = $"S{episode.Episode.Season:00}E{episode.Episode.Number:00} {stamp}\r\n";
                            if ((value.Length + row.Length) > 1000) break;
                            value.Append(row);
                        }

                        builder.AddField(show.Title, value.ToString());
                    }

                    await save.Channel.SendMessageAsync(airingToday.Count == 0 ? null : MentionUtils.MentionUser(userId), embed: builder.Build());
                } catch(Exception ex)
                {
                    Error(ex, userId.ToString());
                }
            }
        }
    }

    public class TraktSave
    {
        [JsonProperty("users")]
        public Dictionary<ulong, TraktUserSave> Users { get; set; } = new Dictionary<ulong, TraktUserSave>();

        [JsonProperty("shows")]
        public Dictionary<int, string> shows { get; set; } = new Dictionary<int, string>();
    }

    public class TraktUserSave
    {
        [JsonProperty("channel")]
        public ITextChannel Channel { get; set; }

        [JsonProperty("token")]
        public TraktAccessToken AccessToken { get; set; }

    }

    public class TraktAccessToken
    {
        [JsonProperty("at")]
        public string AccessToken { get; set; }
        [JsonProperty("rt")]
        public string RefreshToken { get; set; }
        [JsonProperty("iss")]
        public DateTime IssuedAt { get; set; }

        [JsonIgnore]
        public bool Expired => (DateTime.Now - IssuedAt).TotalDays >= 90; // three months

        public async Task<string> GetToken(TraktService service)
        {
            if(Expired)
            {
                var updt = await service.RefreshToken(RefreshToken);
                AccessToken = updt.AccessToken;
                RefreshToken = updt.RefreshToken;
                IssuedAt = updt.IssuedAt;
            }
            return AccessToken;
        }
    }

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class TraktAiringInfo
    {
        [JsonProperty("first_aired")]
        public DateTimeOffset FirstAired { get; set; }
        [JsonProperty("episode")]
        public TraktEpisodeInfo Episode { get; set; }
        [JsonProperty("show")]
        public TraktShowInfo Show { get; set; }

        private string GetDebuggerDisplay()
            => $"{FirstAired:yyyy-MM-dd} {Show.Title} S{Episode.Season:00}E{Episode.Number:00}";
    }

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class TraktIdsInfo
    {
        [JsonProperty("trakt")]
        public int TraktId { get; set; }
        [JsonProperty("tvdb")]
        public int? TvDBId { get; set; }
        [JsonProperty("imdb")]
        public string ImDBId { get; set; }
        [JsonProperty("tmdb")]
        public int? TmDBId { get; set; }

        protected StringBuilder debuggerAppend(StringBuilder builder, string name, string value)
        {
            if(value != null)
            {
                builder.Append($"{name}={value}; ");
            }
            return builder;
        }
        protected StringBuilder debuggerAppend(StringBuilder builder, string name, int? value)
        {
            if (value.HasValue)
            {
                builder.Append($"{name}={value.Value}; ");
            }
            return builder;
        }

        protected virtual string GetDebuggerDisplay()
        {
            var sb = new StringBuilder();
            debuggerAppend(sb, nameof(TraktId), TraktId);
            debuggerAppend(sb, nameof(TvDBId), TvDBId);
            debuggerAppend(sb, nameof(ImDBId), ImDBId);
            debuggerAppend(sb, nameof(TmDBId), TmDBId);
            return sb.ToString();
        }
    }
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class TraktShowIdsInfo : TraktIdsInfo
    {
        [JsonProperty("slug")]
        public string Slug { get; set; }

        protected override string GetDebuggerDisplay()
        {
            return base.GetDebuggerDisplay() + $"; {nameof(Slug)}={Slug}";
        }

    }

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class TraktEpisodeInfo
    {
        [JsonProperty("season")]
        public int Season { get; set; }
        [JsonProperty("number")]
        public int Number { get; set; }
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("ids")]
        public TraktIdsInfo Ids { get; set; }

        private string GetDebuggerDisplay()
        {
            return $"S{Season:00}E{Number:00} {Title}";
        }
    }

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class TraktShowInfo
    {
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("year")]
        public int Year { get; set; }
        [JsonProperty("ids")]
        public TraktShowIdsInfo Ids { get; set; }

        private string GetDebuggerDisplay()
        {
            return $"{Title} ({Year:0000})";
        }
    }
}
