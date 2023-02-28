using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Discord;
using Discord.Interactions;
using DiscordBot.Classes;
using DiscordBot.Utils;
using JsonSubTypes;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DiscordBot.Services.arr
{
    public class WatcherService : SavedService
    {
        public Sonarr.SonarrWebhooksService SonarrWebhooksService { get; set; }
        public Radarr.RadarrWebhookService RadarrWebhookService { get; set; }

        public string BaseUrl { get; set; }
        public string JellyfinApiKey { get; set; }
        public ConcurrentDictionary<ulong, string> UserIds { get; set; } = new();
        public BotHttpClient HTTP
        {
            get
            {
                return Program.GlobalServices.GetRequiredService<BotHttpClient>()
                    .Child("WatcherService", Program.BOT_DEBUG);
            }
        }

        class _save
        {
            public Dictionary<ulong, string> users { get; set; } = new();
        }

        public override void OnReady()
        {
            JellyfinApiKey = EnsureConfiguration("tokens:jellyfin");
            BaseUrl = EnsureConfiguration("urls:jellyfin");
            var sv = Program.Deserialise<_save>(ReadSave());
            UserIds = new ConcurrentDictionary<ulong, string>(sv.users ?? new Dictionary<ulong, string>());
        }

        public override string GenerateSave()
        {
            var sv = new _save();
            sv.users = new Dictionary<ulong, string>(UserIds);

            return Program.Serialise(sv);
        }

        async Task<List<AutocompleteResult>> sonarrSuggest(string showName)
        {
            var ls = new List<AutocompleteResult>();
            var shows = await SonarrWebhooksService.GetShows();
            foreach(var x in shows)
            {
                var name = $"{x.Title} ({x.Year})";
                if(name.Contains(showName, StringComparison.OrdinalIgnoreCase))
                {
                    ls.Add(new AutocompleteResult(name, x.Id));
                    if (ls.Count >= 20) break;
                }
            }
            return ls;
        }

        async Task<List<AutocompleteResult>> radarrSuggest(string movieName)
        {
            var ls = new List<AutocompleteResult>();
            var shows = await RadarrWebhookService.GetMovies();
            foreach (var x in shows)
            {
                var name = $"{x.Title} ({x.Year})";
                if (name.Contains(movieName, StringComparison.OrdinalIgnoreCase))
                {
                    ls.Add(new AutocompleteResult(name, x.Id));
                    if (ls.Count >= 20) break;
                }
            }
            return ls;
        }

        public ComponentBuilder GetComponents(bool hasNext)
        {
            var builder = new ComponentBuilder();
            builder.WithButton("Update", $"watch:update");
            builder.WithButton($"Complete", $"watch:complete");
            if(hasNext)
                builder.WithButton(customId: "watch:next", style: ButtonStyle.Success, emote: Emotes.FAST_FORWARD);
            return builder;
        }

        /*string getAuthHeader(string BotDbAuthToken)
        {
            var dict = new Dictionary<string, string>();
            dict["MediaBrowser Client"] = "MLAPI";
            dict["Device"] = "MLAPI";
            dict["DeviceId"] = "Bot";
            dict["Version"] = "1.0.0";
            dict["Token"] = BotDbAuthToken;

            var s = new StringBuilder();
            foreach(var keypair in dict)
            {
                s.Append($"{keypair.Key}=\"{keypair.Value}\", ");
            }
            s.Remove(s.Length - 3, 2);
            return s.ToString();
        }*/

        public string GetItemIdFromUrl(string url)
            => new Uri(url).AbsolutePath.Split('/')[2];

        public string GetApiKeyFromUrl(string url)
        {
            var uri = new Uri(url);
            return uri.Query.Substring(uri.Query.IndexOf('=') + 1);
        }

        public string GetItemDownloadUrl(string authKey, string itemId)
        {
            return $"{BaseUrl}/Items/{itemId}/Download?api_key={authKey}";
        }

        Task<HttpResponseMessage> request(HttpMethod method, string authKey, string uri)
        {
            //var auth = getAuthHeader(authKey);
            var request = new HttpRequestMessage(method, BaseUrl + uri
                );
            request.Headers.Add("X-Emby-Token", authKey);
            return HTTP.SendAsync(request);
        }

        public async Task<JellyfinItem[]> GetPlaylistItems(string playlistId, JellyfinAuth auth)
        {
            var response = await request(HttpMethod.Get, auth.AuthKey, $"/Playlists/{playlistId}/Items?userId={auth.UserId}");
            var content = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            var jobj = JObject.Parse(content);
            var items = jobj["Items"] as JArray;
            return items.ToObject<JellyfinItem[]>();
        }

        public async Task<JellyfinItem> GetItemInfo(string itemId, JellyfinAuth auth)
        {
            var response = await request(HttpMethod.Get, auth.AuthKey, $"/Users/{auth.UserId}/Items/{itemId}"
                /*$"&SortBy=DatePlayed" +
                $"&IncludeItemTypes=Movie,Series" +
                $"&Limit=20" +
                $"&Recursive=true" +
                $"&ImageTypeLimit=0" +
                $"&EnableImages=false" +
                $"&EnableTotalRecordCount=false" +
                $"&Fields=Overview" +*/
                );
            var content = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            return Program.Deserialise<JellyfinItem>(content);
        }

        private CacheDictionary<string, JellyfinUser> _userCache = new();
        public async Task<JellyfinUser> GetUserFromAuthKey(string authKey)
        {
            if (authKey == JellyfinApiKey) return null;
            if (_userCache.TryGetValue(authKey, out var j))
                return j;
            var response = await request(HttpMethod.Get, authKey, "/Users/Me");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            j = JsonConvert.DeserializeObject<JellyfinUser>(content);
            _userCache[authKey] = j;
            return j;
        }

        public async Task MarkWatched(string jellyId, JellyfinAuth auth)
        {
                                                   // 2022-05-10T16%3A35%3A29.128Z
            var played = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var reqUrl = $"/Users/{auth.UserId}/PlayedItems/{jellyId}?DatePlayed={played}";

            var response = await request(HttpMethod.Post, auth.AuthKey, reqUrl);

            response.EnsureSuccessStatusCode();
        }

        public async Task<JellyfinItem[]> GetNextUp(JellyfinAuth auth, string seriesId = null)
        {
            var uriB = new UrlBuilder("/Shows/NextUp");
            uriB.Add("userId", auth.UserId);
            if (seriesId != null)
                uriB.Add("seriesId", seriesId);
            var response = await request(HttpMethod.Get, auth.AuthKey, uriB.ToString());
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var jobj = JObject.Parse(content);
            var items = jobj["Items"] as JArray;
            return items.ToObject<JellyfinItem[]>();

        }

        public async Task SetWatchedTime(string itemId, ulong ticks, string playSessionId, JellyfinAuth auth)
        {
            var response = await request(HttpMethod.Post, auth.AuthKey, $"/Users/{auth.UserId}/PlayingItems/{itemId}/Progress" +
                $"?positionTicks={ticks}" + (playSessionId == null ? "" : $"&playSessionId={playSessionId}"));
        }

        public async Task<JellyfinSession> GetFirstCapableSession(JellyfinAuth auth)
        {
            var resp = await request(HttpMethod.Get, auth.AuthKey, $"/Sessions?controllableByUser={auth.UserId}");
            var cont = await resp.Content.ReadAsStringAsync();
            var sessions = Program.Deserialise<JellyfinSession[]>(cont);
            return sessions.Where(x => x.IsActive && x.PlayableMediaTypes.Length > 0).FirstOrDefault();
        }



        public class Handler : AutocompleteHandler
        {
            public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
            {
                var service = services.GetRequiredService<WatcherService>();
                var current = autocompleteInteraction.Data.Current;
                var firstOption = autocompleteInteraction.Data.Options.First();
                if(firstOption.Name == "movie")
                {
                    var suggestions = await service.radarrSuggest(current.Value as string);
                    return AutocompletionResult.FromSuccess(suggestions);
                } else
                {
                    var suggestions = await service.sonarrSuggest(current.Value as string);
                    return AutocompletionResult.FromSuccess(suggestions);
                }
            }
        }

        public class JellyfinAuth
        {
            private JellyfinAuth(string key, string id)
            {
                AuthKey = key;
                UserId = id;
            }
            public string AuthKey { get; set; }
            public string UserId { get; }

            public static async Task<JellyfinAuth> Parse(string token, ulong executingId, WatcherService service)
            {
                string authKey;
                if(token.Contains("/Download"))
                {
                    authKey = service.GetApiKeyFromUrl(token);
                } else
                {
                    authKey = service.JellyfinApiKey;
                }
                if(!service.UserIds.TryGetValue(executingId, out var userId))
                {
                    var usr = await service.GetUserFromAuthKey(authKey);
                    if (usr != null)
                    {
                        userId = usr.Id;
                        service.UserIds[executingId] = userId;
                        service.OnSave();
                    }
                }
                return new JellyfinAuth(authKey, userId);

            }
        }

        [JsonConverter(typeof(JsonSubtypes), "Type")]
        [JsonSubtypes.KnownSubType(typeof(JellyfinEpisodeItem), "Episode")]
        [JsonSubtypes.KnownSubType(typeof(JellyfinMovieItem), "Movie")]
        [JsonSubtypes.KnownSubType(typeof(JellyfinSeriesItem), "Series")]
        [JsonSubtypes.KnownSubType(typeof(JellyfinPlaylist), "Playlist")]
        public abstract class JellyfinItem
        {
            public string Name { get; set; }
            public string ServerId { get; set; }
            public string Id { get ; set; }
            public DateTime? PremiereDate { get; set; }

            public int ProductionYear { get; set; }

            public abstract Task<EmbedBuilder> ToEmbed(WatcherService service, JellyfinAuth auth);
        }

        public class JellyfinEpisodeItem : JellyfinItem
        {
            public int IndexNumber { get; set; }
            public int ParentIndexNumber { get; set; }
            public string SeriesName { get; set; }
            public string SeriesId { get; set; }

            public JellyfinPlaylistUserData UserData { get; set; }

            public override Task<EmbedBuilder> ToEmbed(WatcherService service, JellyfinAuth auth)
            {

                var builder = new EmbedBuilder();
                builder.Url = service.GetItemDownloadUrl(auth.AuthKey, Id);
                builder.Title = $"{SeriesName} S{ParentIndexNumber:00}E{IndexNumber:00}";
                if (!string.IsNullOrWhiteSpace(Name))
                    builder.Description = $"*{Name}*\n";
                builder.Color = Color.Blue;
                builder.WithFooter(SeriesId);

                return Task.FromResult(builder);
            }
        }

        public class JellyfinPlaylistUserData
        {
            public double? PlayedPercentage { get; set; }
            public int? UnplayedItemCount { get; set; }
            public ulong? PlaybackPositionTicks { get; set; }
            public int? PlayCount { get; set; }
            public bool? IsFavourite { get; set; }
            public bool? Played { get; set; }
            public string Key { get; set; }
        }

        public class JellyfinPlaylist : JellyfinItem
        {
            public int ChildCount { get; set; }
            public JellyfinPlaylistUserData UserData { get; set; }

            private JellyfinItem[] items = null;
            public override async Task<EmbedBuilder> ToEmbed(WatcherService service, JellyfinAuth auth)
            {
                var builder = new EmbedBuilder();
                builder.Color = Color.Red;
                builder.WithFooter(Id);
                items ??= await service.GetPlaylistItems(Id, auth);

                JellyfinItem firstNonPlayed = null;
                foreach(var item in items)
                {
                    if (item is JellyfinEpisodeItem ep && !ep.UserData.Played.GetValueOrDefault(false))
                    {
                        firstNonPlayed = ep;
                        break;
                    }
                    else if (item is JellyfinMovieItem mv && !mv.UserData.Played.GetValueOrDefault(false))
                    {
                        firstNonPlayed = mv;
                        break;
                    }
                }
                if(firstNonPlayed == null)
                {
                    builder.Title = this.Name;
                    builder.Description = "No unplayed items\r\n";
                } else
                {
                    var itemEmbed = await firstNonPlayed.ToEmbed(service, auth);
                    builder.Url = itemEmbed.Url;
                    builder.Title = itemEmbed.Title;
                    builder.Description = itemEmbed.Description + $"\r\nPlaylist *{Name}*; {UserData.PlayedPercentage.GetValueOrDefault(0):00}%\r\n";
                }

                return builder;
            }

            public async Task<FileAttachment> ToPlaylistFile(WatcherService service, JellyfinAuth auth, bool skipWatched = true)
            {
                var fileName = $"playlist.xspf";
                var path = Path.Combine(Path.GetTempPath(), fileName);
                items ??= await service.GetPlaylistItems(Id, auth);

                using (var xmlWriter = XmlWriter.Create(path, new XmlWriterSettings()
                {
                    Indent = true,
                    Async = true
                }))
                {
                    await xmlWriter.WriteStartDocumentAsync();

                    await xmlWriter.WriteStartElementAsync(null, "playlist", null);
                    await xmlWriter.WriteAttributeStringAsync(null, "version", null, "1");

                    await xmlWriter.WriteStartElementAsync(null, "trackList", null);

                    foreach(var item in items)
                    {

                        string title;
                        JellyfinPlaylistUserData data = null;
                        if(item is JellyfinEpisodeItem ep)
                        {
                            data = ep.UserData;
                            title = $"{ep.SeriesName}: S{ep.ParentIndexNumber:00}E{ep.IndexNumber:00} {ep.Name}";
                        } else if (item is JellyfinMovieItem mv)
                        {
                            data = mv.UserData; 
                            title = $"{item.Name} ({item.ProductionYear:0000})";
                        }
                        else
                        {
                            title = $"{item.Name}";
                        }
                        if(data != null)
                        {
                            if (skipWatched && data.Played.GetValueOrDefault(false)) continue;

                        }
                        await xmlWriter.WriteStartElementAsync(null, "track", null);

                        await xmlWriter.WriteElementStringAsync(null, "title", null, title);
                        await xmlWriter.WriteElementStringAsync(null, "location", null, service.GetItemDownloadUrl(auth.AuthKey, item.Id));

                        await xmlWriter.WriteEndElementAsync(); // track
                    }


                    await xmlWriter.WriteEndElementAsync(); // trackList
                    await xmlWriter.WriteEndElementAsync(); // playlist

                    await xmlWriter.FlushAsync();
                }
                return new FileAttachment(path, fileName, $"Playlist for {Name}");
            }
        }

        public class JellyfinMovieItem : JellyfinItem
        {
            public JellyfinPlaylistUserData UserData { get; set; }

            public override Task<EmbedBuilder> ToEmbed(WatcherService service, JellyfinAuth auth)
            {

                var builder = new EmbedBuilder();
                builder.Url = service.GetItemDownloadUrl(auth.AuthKey, Id);
                builder.Title = $"{Name} ({ProductionYear})";
                builder.Color = Color.Gold;

                return Task.FromResult(builder);
            }
        }
        public class JellyfinSeriesItem : JellyfinItem
        {
            public override Task<EmbedBuilder> ToEmbed(WatcherService service, JellyfinAuth auth)
            {
                return null;
            }
        }

        public class JellyfinUser
        {
            public string Id { get; set; }
            public string Name { get; set; }

            [JsonExtensionData]
            public Dictionary<string, JToken> Excess { get; set; }
        }

        public class JellyfinSession
        {
            public string Id { get; set; }
            public string[] PlayableMediaTypes { get; set; }

            public bool IsActive { get; set; }
        }

        public class JellyfinKey
        {
            public int Id { get; set; }
            public string AccessToken { get; set; }

            public string DeviceId { get; set; }

            public string UserId { get; set; }

            public bool IsActive { get; set; }

            public string UserName { get; set; }
        }
    }
}
