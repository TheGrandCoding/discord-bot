using Discord;
using DiscordBot.Classes;
using DiscordBot.Services.Sonarr;
using JsonSubTypes;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;


namespace DiscordBot.Services.Radarr
{
    public class RadarrWebhookService : SavedService, Services.BuiltIn.IRegisterable
    {
        public List<SaveChannel> Channels { get; set; }


        const string apiUrl = "https://radarr.cheale14.com/api/v3";
        public Semaphore Lock = new Semaphore(1, 1);
        public BotHttpClient HTTP { get; private set; }
        public TraktService Trakt { get; private set; }
        public CacheDictionary<int, string> TagsCache { get; } = new CacheDictionary<int, string>(60 * 24);
        public CacheDictionary<int, string[]> MovieTagsCache { get; } = new CacheDictionary<int, string[]>(60 * 24); // day

        public override string GenerateSave()
        {
            var save = new Save()
            {
                Channels = Channels
            };
            return Program.Serialise(save);
        }
        public override void OnReady(IServiceProvider services)
        {
            var save = Program.Deserialise<Save>(ReadSave("{}"));
            Channels = save.Channels ?? new List<SaveChannel>();
            Trakt = Program.GlobalServices.GetRequiredService<TraktService>();
        }
        public override void OnLoaded(IServiceProvider services)
        {
            var parent = Program.GlobalServices.GetRequiredService<BotHttpClient>();
            HTTP = parent.Child("RadarrAPI");
            var apiKey = Program.Configuration["tokens:radarr"];
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException("Radarr API Key missing at tokens:sonarr");
            HTTP.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            OnGrab += RadarrWebhookService_OnGrab;
            OnDownload += RadarrWebhookService_OnDownload;
        }

        private void RadarrWebhookService_OnDownload(object sender, OnDownloadRadarrEvent e)
        {
            HandleRadarrOnDownload(e).Wait();
        }

        async Task HandleRadarrOnDownload(OnDownloadRadarrEvent e)
        {
            try
            {
                await Trakt.CollectNew(e);
            }
            catch (Exception ex)
            {
                ErrorToOwner(ex, "RadarrDL");
            }
            var embed = new EmbedBuilder();
            var movie = await GetMovie(e.Movie.Id);
            embed.Title = $"{movie.Title} {movie.Year}";
            embed.Color = Color.Green;
            embed.WithFooter("Movie Imported");
            embed.Description = Program.Clamp(movie.Overview, 256);
            embed.ImageUrl = movie.Images.First().RemoteUrl;
            if (!string.IsNullOrWhiteSpace(e.MovieFile.Quality))
                embed.AddField("Quality", e.MovieFile.Quality, true);
            foreach (var chnl in Channels)
            {
                var shouldSend = await ShouldSendInChannel(e.Movie.Id, chnl);
                if (shouldSend)
                {
                    await chnl.Channel.SendMessageAsync(embed: embed.Build());
                }
            }
        }

        private void RadarrWebhookService_OnGrab(object sender, OnGrabRadarrEvent e)
        {
            new Thread(handleOnSeparateThread).Start(e);
        }

        void handleOnSeparateThread(object o)
        {
            if (!(o is OnGrabRadarrEvent e))
                return;
            try
            {
                HandleRadarrOnGrab(e).Wait();
            } catch(Exception ex)
            {
                Program.LogError(ex, "Radarr " + e.DownloadId ?? "");
            }
        }

        async Task HandleRadarrOnGrab(OnGrabRadarrEvent e)
        {
            var embed = new EmbedBuilder();
            var movie = await GetMovie(e.Movie.Id);
            var history = await GetHistory(e.Movie.Id);
            embed.Title = $"{movie.Title} {movie.Year}";
            embed.WithFooter("Movie Grabbed");
            embed.Color = Color.Orange;
            embed.Description = Program.Clamp(movie.Overview, 256);
            embed.ImageUrl = movie.Images.First().RemoteUrl;
            if (!string.IsNullOrWhiteSpace(e.Release.Quality))
                embed.AddField("Quality", e.Release.Quality, true);
            string relStr;
            if(history == null)
            {
                relStr = "*Failed to get release*";
            } else
            {
                relStr = $"[{e.Release.ReleaseTitle}]({history.Data.NzbInfoUrl})";
            }
            embed.AddField("Release", relStr, true);
            foreach (var chnl in Channels)
            {
                var shouldSend = await ShouldSendInChannel(e.Movie.Id, chnl);
                if (shouldSend)
                {
                    await chnl.Channel.SendMessageAsync(embed: embed.Build());
                }
            }
        }

        async Task<RadarrGrabbedHistoryRecord> GetHistory(int movieId, int attempts = 0)
        {
            var url = apiUrl + $"/history/movie?movieId={movieId}&eventType={1}"; // 1=grabbed
            Info($"GET{attempts} {url}", "RadarrGetHistory");
            var response = await HTTP.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            var array = JsonConvert.DeserializeObject<List<RadarrHistoryRecord>>(content);
            Info($"GET {url} :: {response.StatusCode}, {(array?.Count ?? -1)} items", "RadarrGetHistory");
            foreach(var record in array.OrderByDescending(x => x.Date))
            {
                if (record is RadarrGrabbedHistoryRecord gh)
                    return gh;
            }
            Info($"Failed to find history for {movieId}, waiting then retrying.", "RadarrGetHistory");
            if (attempts > 10)
            {
                Info($"{movieId} not retrying - too many attempts.", "RadarrGetHistory");
                return null;
            }
            Thread.Sleep(1000 * attempts);
            return await GetHistory(movieId, attempts + 1);
        }

        public async Task<MovieInfo[]> GetMovies()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl + $"/movie");
            var response = await HTTP.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<MovieInfo[]>(content);
        }

        async Task<string> GetTagLabel(int id)
        {
            if (TagsCache.TryGetValue(id, out var s))
                return s;
            var response = await HTTP.GetAsync(apiUrl + $"/tag/{id}");
            var jobj = JObject.Parse(await response.Content.ReadAsStringAsync());
            var l = jobj["label"].ToObject<string>();
            TagsCache[id] = l;
            return l;
        }

        public async Task<MovieInfo> GetMovie(int movieId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl + $"/movie/{movieId}");
            var response = await HTTP.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            Info($"{movieId} :: {response.StatusCode}", "GetMovie");
            var parsed = Program.Deserialise<MovieInfo>(content);
            return parsed;
        }

        public async Task<string[]> GetMovieTags(int movieId)
        {
            if (MovieTagsCache.TryGetValue(movieId, out var v))
                return v;
            var movie = await GetMovie(movieId);
            var parsed = new List<string>();
            foreach (var tag in movie.Tags)
            {
                var label = await GetTagLabel(tag);
                parsed.Add(label);
            }
            MovieTagsCache.Add(movieId, parsed.ToArray());
            Info($"For {movieId}: [{string.Join(", ", parsed)}]", "GetMovieTags");
            return parsed.ToArray();
        }

        public async Task<bool> MovieHasTag(int movieId, Func<string, bool> tagPredicate)
        {
            foreach (var tag in await GetMovieTags(movieId))
            {
                if (tagPredicate(tag))
                    return true;
            }
            return false;
        }

        public async Task<bool> ShouldSendInChannel(int movieId, SaveChannel channel)
        {
            var tags = await GetMovieTags(movieId);
            if (tags.Contains("private") && channel.ShowsPrivate == false)
                return false;
            foreach (var required in channel.TagRequired)
                if (tags.Contains(required))
                    return true;
            return channel.TagRequired.Count == 0;
        }
        public event EventHandler<OnTestRadarrEvent> OnTest;
        public event EventHandler<OnGrabRadarrEvent> OnGrab;
        public event EventHandler<OnDownloadRadarrEvent> OnDownload;

        public void Handle(RadarrEvent type)
        {
            Info($"Waiting lock for {type.EventType} | {typeof(RadarrEvent)}", "OnGrab");
            Lock.WaitOne();
            Info($"Received lock for {type.EventType}", "OnGrab");
            try
            {
                if (type is OnTestRadarrEvent t)
                    OnTest?.Invoke(this, t);
                else if (type is OnGrabRadarrEvent g)
                {
                    Info($"Invoking event for OnGrab", "OnGrab");
                    Info($"{OnGrab?.GetInvocationList().Length} listeners #4", "OnGrab");
                    OnGrab?.Invoke(this, g);
                }
                else if (type is OnDownloadRadarrEvent d)
                    OnDownload?.Invoke(this, d);
            }
            finally
            {
                Lock.Release();
                Info($"Released lock for {type.EventType}", "OnGrab");
            }
        }


        public Task<string> RegisterAsync(IMessageChannel channel, IUser user)
        {
            if (Channels.Any(x => x.Channel.Id == channel.Id))
                return Task.FromResult(":x: This channel is already registered.");
            var sv = new SaveChannel()
            {
                Channel = channel as ITextChannel,
                ShowsPrivate = false
            };
            Channels.Add(sv);
            OnSave();
            return null;
        }

        public Task<string> UnregisterAsync(IMessageChannel channel, IUser user)
        {
            if (Channels.RemoveAll(x => x.Channel.Id == channel.Id) > 0)
            {
                OnSave();
                return null;
            }
            return Task.FromResult("This channel was not registered to begin with.");
        }
    }

    #region Events
    [JsonConverter(typeof(JsonSubtypes), "EventType")]
    [JsonSubtypes.KnownSubType(typeof(OnGrabRadarrEvent), "Grab")]
    [JsonSubtypes.KnownSubType(typeof(OnDownloadRadarrEvent), "Download")]
    [JsonSubtypes.KnownSubType(typeof(OnTestRadarrEvent), "Test")]
    public abstract class RadarrEvent
    {
        public string EventType { get; set; }
        public StubMovieInfo Movie { get; set; }
        public RemoteMovieInfo RemoteMovie { get; set; }
    }

    public class OnGrabRadarrEvent : RadarrEvent
    {
        public string DownloadClient { get; set; }
        public string DownloadId { get; set; }
        public MovieReleaseInfo Release { get; set; }
    }

    public class OnDownloadRadarrEvent : RadarrEvent
    {
        public string DownloadId { get; set; }
        public MovieFile MovieFile { get; set; }
        public bool isUpgrade { get; set; }
    }

    public class OnTestRadarrEvent : RadarrEvent
    {
    }
    #endregion

    #region Entities
    public class StubMovieInfo
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string FolderPath { get; set; }
        public int TmdbId { get; set; }
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class MovieInfo : StubMovieInfo
    {
        public string SortTitle { get; set; }
        public long SizeOnDisk { get; set; }
        public string Overview { get; set; }
        public string InCinemas { get; set; }
        public string PhysicalRelease { get; set; }
        public MovieImage[] Images { get; set; }
        public string Website { get; set; }
        public int Year { get; set; }
        public bool HasFile { get; set; }
        public string YouTubeTrailerId { get; set; }
        public string Studio { get; set; }
        public string Path { get; set; }
        public string RootFolderPath { get; set; }
        public int QualityProfileId { get; set; }
        public bool Monitored { get; set; }
        public int Runtime { get; set; }
        public string[] Genres { get; set; }
        public int[] Tags { get; set; }

        private string GetDebuggerDisplay()
        {
            return $"{Title} {Year} {TmdbId}";
        }
    }
    public class MovieImage
    {
        public string CoverType { get; set; }
        public string Url { get; set; }
        public string RemoteUrl { get; set; }
    }

    public class RemoteMovieInfo
    {
        public int TmdbId { get; set; }
        public string ImdbId { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
    }
    public class MovieReleaseInfo
    {
        public string Quality { get; set; }
        public int QualityVersion { get; set; }
        public string ReleaseGroup { get; set; }
        public string ReleaseTitle { get; set; }
        public string Indexer { get; set; }
        public long Size { get; set; }
    }
    public class MovieFile
    {
        public int Id { get; set; }
        public string RelativePath { get; set; }
        public string Path { get; set; }
        public string Quality { get; set; }
        public int QualityVersion { get; set; }
        public long Size { get; set; }
    }
    [JsonConverter(typeof(JsonSubtypes), "EventType")]
    [JsonSubtypes.KnownSubType(typeof(RadarrGrabbedHistoryRecord), "grabbed")]
    [JsonSubtypes.FallBackSubType(typeof(RadarrGenericHistoryRecord))]
    public abstract class RadarrHistoryRecord
    {
        public int Id { get; set; }
        public int MovieId { get; set; }
        public string SoruceTitle { get; set; }
        public JArray Languages { get; set; }
        public SonarrQuality Quality { get; set; }
        public string EventType { get; set; }
        public DateTime Date { get; set; }
    }
    public class RadarrGrabbedInfo
    {
        public string Indexer { get; set; }
        public string NzbInfoUrl { get; set; }
        public string ReleaseGroup { get; set; }
        [JsonProperty("age")]
        public int AgeDays { get; set; }
        public double AgeHours { get; set; }
        public double AgeMinutes { get; set; }
        public DateTime PublishedDate { get; set; }
        public long Size { get; set; }
        public string DownloadUrl { get; set; }
        public string Guid { get; set; }
    }
    public class RadarrGrabbedHistoryRecord : RadarrHistoryRecord
    {
        public RadarrGrabbedInfo Data { get; set; }
    }
    public class RadarrGenericHistoryRecord : RadarrHistoryRecord
    {
        public JObject Data { get; set; }
    }
    #endregion
}
