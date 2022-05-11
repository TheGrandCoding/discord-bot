using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using DiscordBot.Classes;
using JsonSubTypes;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DiscordBot.Services.arr
{
    public class WatcherService : Service
    {
        public Sonarr.SonarrWebhooksService SonarrWebhooksService { get; set; }
        public Radarr.RadarrWebhookService RadarrWebhookService { get; set; }

        public string BaseUrl { get; set; }
        public BotHttpClient HTTP
        {
            get
            {
                return Program.Services.GetRequiredService<BotHttpClient>()
                    .Child("WatcherService", Program.BOT_DEBUG);
            }
        }

        public override void OnReady()
        {
            BaseUrl = Program.Configuration["urls:jellyfin"];
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

        public EmbedBuilder GetEmbed(string url, string title, string progress)
        {
            var builder = new EmbedBuilder();
            builder.Url = url;
            builder.Title = title;
            builder.Description = $"{progress}";

            return builder;
        }
        public ComponentBuilder GetComponents()
        {
            var builder = new ComponentBuilder();
            builder.WithButton("Update", $"watch:update");
            builder.WithButton($"Complete", $"watch:complete");
            return builder;
        }

        string getAuthHeader(string authToken)
        {
            var dict = new Dictionary<string, string>();
            dict["MediaBrowser Client"] = "MLAPI";
            dict["Device"] = "MLAPI";
            dict["DeviceId"] = "Bot";
            dict["Version"] = "1.0.0";
            dict["Token"] = authToken;

            var s = new StringBuilder();
            foreach(var keypair in dict)
            {
                s.Append($"{keypair.Key}=\"{keypair.Value}\", ");
            }
            s.Remove(s.Length - 3, 2);
            return s.ToString();
        }

        public string GetItemIdFromUrl(string url)
            => new Uri(url).AbsolutePath.Split('/')[2];

        public string GetApiKeyFromUrl(string url)
        {
            var uri = new Uri(url);
            return uri.Query.Substring(uri.Query.IndexOf('=') + 1);
        }

        public async Task<JellyfinItem> GetItemInfo(string authKey, string itemId)
        {
            var auth = getAuthHeader(authKey);
            var user = await GetUserFromAuthKey(authKey);
            var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + 
                $"/Items?ids={itemId}" +
                /*$"&SortBy=DatePlayed" +
                $"&IncludeItemTypes=Movie,Series" +
                $"&Limit=20" +
                $"&Recursive=true" +
                $"&ImageTypeLimit=0" +
                $"&EnableImages=false" +
                $"&EnableTotalRecordCount=false" +
                $"&Fields=Overview" +*/
                $"&UserId={user["Id"]}");
            request.Headers.Add("X-Emby-Authorization", auth);
            var response = await HTTP.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            var obj = JObject.Parse(content);
            var arr = obj["Items"] as JArray;
            return arr[0].ToObject<JellyfinItem>();
        }

        public async Task<JObject> GetUserFromAuthKey(string authKey)
        {
            var auth = getAuthHeader(authKey);
            var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "/Users/Me");
            request.Headers.Add("X-Emby-Authorization", auth);
            var response = await HTTP.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content);
        }

        public async Task MarkWatched(string authKey, string jellyId)
        {
            var auth = getAuthHeader(authKey);
            var user = await GetUserFromAuthKey(authKey);
            var id = user["Id"].ToObject<string>();
            // 2022-05-10T16%3A35%3A29.128Z
            var played = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var reqUrl = $"/Users/{id}/PlayedItems/{jellyId}?DatePlayed={played}";
            var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + reqUrl);
            request.Headers.Add("X-Emby-Authorization", auth);
            var response = await HTTP.SendAsync(request);
            response.EnsureSuccessStatusCode();
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

        [JsonConverter(typeof(JsonSubtypes), "Type")]
        [JsonSubtypes.KnownSubType(typeof(JellyfinEpisodeItem), "Episode")]
        [JsonSubtypes.KnownSubType(typeof(JellyfinMovieItem), "Movie")]
        public class JellyfinItem
        {
            public string Name { get; set; }
            public string ServerId { get; set; }
            public string Id { get ; set; }
            public DateTime PremiereDate { get; set; }

            public int ProductionYear { get; set; }
        }

        public class JellyfinEpisodeItem : JellyfinItem
        {
            public int IndexNumber { get; set; }
            public int ParentIndexNumber { get; set; }
            public string SeriesName { get; set; }
            public string SeriesId { get; set; }
        }
        public class JellyfinMovieItem : JellyfinItem
        {
        }
    }
}
