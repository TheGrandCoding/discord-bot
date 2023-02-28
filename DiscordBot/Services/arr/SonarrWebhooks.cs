using Discord;
using DiscordBot.Classes;
using DiscordBot.Classes.Attributes;
using JsonSubTypes;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services.Sonarr
{
    [RequireService(typeof(TraktService))]
    public class SonarrWebhooksService : SavedService
    {
        public List<SaveChannel> Channels { get; set; }

        const string apiUrl = "https://sonarr.cheale14.com/api";

        public Semaphore Lock = new Semaphore(1, 1);
        public BotHttpClient HTTP { get; private set; }
        public TraktService Trakt { get; private set; }
        public CacheDictionary<int, string> TagsCache { get; } = new CacheDictionary<int, string>(60 * 24);
        public CacheDictionary<int, string[]> SeriesTagsCache { get; } = new CacheDictionary<int, string[]>(60 * 24); // day

        public override string GenerateSave()
        {
            var save = new Save()
            {
                Channels = Channels
            };
            return Program.Serialise(save);
        }
        public override void OnReady()
        {
            var save = Program.Deserialise<Save>(ReadSave("{}"));
            Channels = new List<SaveChannel>();
            foreach(var chnl in (save.Channels ?? new List<SaveChannel>()))
            {
                if (!(chnl.Channel is NullTextChannel))
                    Channels.Add(chnl);
            }
            Trakt = Program.GlobalServices.GetRequiredService<TraktService>();
        }

        const string CONFIG_API_KEY = "tokens:sonarr";
        public override void OnLoaded()
        {
            EnsureConfiguration(CONFIG_API_KEY);
            var parent = Program.GlobalServices.GetRequiredService<BotHttpClient>();
            HTTP = parent.Child("SonarrAPI");
            var apiKey = Program.Configuration[CONFIG_API_KEY];
            HTTP.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            var th = new Thread(loop);
            th.Start(Program.GetToken());
            OnDownload += SonarrWebhooksService_OnDownload;
            OnGrab += SonarrWebhooksService_OnGrab;
            OnEpisodeFileDelete += SonarrWebhooksService_OnEpisodeFileDelete;
#if DEBUG
            OnTest += (object sender, OnTestSonarrEvent e) =>
            {
                Episodes[e.Series.Id] = new Episodes(e.Series, e.Episodes);
            };
#endif
        }

        private void SonarrWebhooksService_OnEpisodeFileDelete(object sender, OnEpisodeFileDeleteSonarrEvent e)
        {
            Task.Run(async () =>
            {
                try
                {
                    foreach (var channel in Channels)
                    {
                        if (channel.Channel is NullTextChannel)
                            continue;
                        var check = await ShouldSendInChannel(e.Series.Id, channel);
                        if (check == false)
                            continue;
                        await channel.Channel.SendMessageAsync($"Removed episode file for {e.Series.Title}, S{e.Episodes[0].SeasonNumber:00}E{e.Episodes[0].EpisodeNumber:00}; {e.DeleteReason}");
                    }
                }
                catch { }
            });
        }

        #region Sonarr API
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

        public async Task<string[]> GetSeriesTags(int seriesId)
        {
            if (SeriesTagsCache.TryGetValue(seriesId, out var v))
                return v;
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl + $"/series/{seriesId}");
            var response = await HTTP.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            Info($"{seriesId} :: {response.StatusCode}");
            var jobj = JObject.Parse(content);
            var array = jobj["tags"].ToObject<string[]>();
            var parsed = new List<string>();
            foreach (var tag in array)
            {
                if (!int.TryParse(tag, out var tagId))
                {
                    parsed.Add(tag);
                    continue;
                }
                var label = await GetTagLabel(tagId);
                parsed.Add(label);
            }
            SeriesTagsCache.Add(seriesId, parsed.ToArray());
            Info($"For {seriesId}: [{string.Join(", ", parsed)}]");
            return parsed.ToArray();
        }
        
        public async Task<List<SonarrSeries>> GetShows()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl + "/series");
            var response = await HTTP.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<Sonarr.SonarrSeries>>(body);
        }

        public async Task<SonarrSeries> GetShow(int id)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl + $"/series/{id}");
            var response = await HTTP.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Sonarr.SonarrSeries>(body);
        }

        public async Task<List<SonarrEpisode>> GetAllEpisodesAsync(int seriesId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl + $"/episode?seriesId={seriesId}");
            var response = await HTTP.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<SonarrEpisode>>(body);
        }

        #endregion



        public async Task<bool> SeriesHasTag(int seriesId, Func<string, bool> tagPredicate)
        {
            foreach (var tag in await GetSeriesTags(seriesId))
            {
                if (tagPredicate(tag))
                    return true;
            }
            return false;
        }

        public async Task<bool> ShouldSendInChannel(int seriesId, SaveChannel channel)
        {
            var tags = await GetSeriesTags(seriesId);
            if (tags.Contains("private") && channel.ShowsPrivate == false)
                return false;
            foreach (var required in channel.TagRequired)
                if (tags.Contains(required))
                    return true;
            return channel.TagRequired.Count == 0;
        }

        private void SonarrWebhooksService_OnGrab(object sender, OnGrabSonarrEvent e)
        {
            new Thread(handleGrabThread).Start(e);
        }

        void handleGrabThread(object o)
        {
            if (!(o is OnGrabSonarrEvent e))
                return;
            Info($"Starting handler", "OnGrab");
            try
            {
                HandleOnGrabAsync(e).Wait();
            }
            catch (Exception ex)
            {
                Error(ex, "OnGrab:" + e.DownloadId ?? "");
            }
            Info($"Finished handler", "OnGrab");
        }

        public async Task<SonarrHistoryGrabbedRecord> GetHistory(int episodeId, int seriesId, int attempts = 0)
        {
            var url = apiUrl + $"/history?sortKey=date&episodeId={episodeId}";
            Info($"Sending GET{attempts} {url}", "OnGrab");
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await HTTP.SendAsync(request);
            Info($"{episodeId} :: {response.StatusCode}");
            var content = await response.Content.ReadAsStringAsync();
            Info($"Parsed content to string", "OnGrab");
            var history = JsonConvert.DeserializeObject<SonarrHistoryCollection>(content);
            Info($"Got {history.Records.Length}/{history.TotalRecords} records", "OnGrab");

            var recentGrab = history.Records
                .Where(x => x.Series.Id == seriesId)
                .FirstOrDefault(x => x is SonarrHistoryGrabbedRecord) as SonarrHistoryGrabbedRecord;
            if (recentGrab != null)
                return recentGrab;
            Info($"Failed to get history for {episodeId}, waiting then retrying");
            if(attempts > 10)
            {
                Info("Too many attempts, exiting");
                return null;
            }
            Thread.Sleep(1000 * attempts);
            return await GetHistory(episodeId, seriesId, attempts + 1);
        }

        public async Task HandleOnGrabAsync(OnGrabSonarrEvent e)
        {
            Info($"Handling {e.Series.Title}", "OnGrab");
            //var episodes = new Dictionary<int, bool>();
            var releases = new List<SonarrGrabbedData>();
            var builder = new EmbedBuilder();
            builder.Title = "Episodes Grabbed";
            builder.Description = $"{e.Series.Title}; {e.Episodes.Length} episodes found";
            if (!string.IsNullOrWhiteSpace(e.Release.Quality))
                builder.Description += " at " + e.Release.Quality;
            builder.Color = Color.Orange;
            var tags = await GetSeriesTags(e.Series.Id);
            builder.WithFooter(string.Join(", ", tags));
            foreach (var episode in e.Episodes)
            {
                //if (episodes.ContainsKey(episode.Id))
                //    continue;
                var recentGrab = await GetHistory(episode.Id, e.Series.Id);
                if (recentGrab == null)
                    continue;
                //episodes[episode.Id] = true;
                var existing = releases.Any(x => x.guid == recentGrab.data.guid);
                if(builder.ImageUrl == null)
                {
                    builder.ImageUrl = (recentGrab.Series.Images.FirstOrDefault(x => x.CoverType == "poster")
                        ?? recentGrab.Series.Images.First()).RemoteUrl;
                }
                Info($"{episode.Id} :: {recentGrab.Series.Title} {recentGrab.data.guid}");
                if (!existing)
                    releases.Add(recentGrab.data);
                if(builder.Fields.Count < 23)
                {
                    builder.AddField($"S{episode.SeasonNumber:00}E{episode.EpisodeNumber:00}", episode.Title, true);
                }
            }
            var relStr = "";
            if(releases.Count == 1)
            {
                relStr = $"[{e.Release.ReleaseTitle}]({releases.First().nzbInfoUrl})";
            } else if (releases.Count == 0)
            {
                relStr = "*Failed to get release*";
            } else {
                foreach(var rel in releases)
                {
                    relStr += $"{rel.nzbInfoUrl}\r\n";
                }
            }
            builder.AddField("Release" + (releases.Count > 1 ? "s" : ""), relStr, false);
            foreach (var channel in Channels) 
            {
                if (channel.Channel is NullTextChannel)
                    continue;
                var check = await ShouldSendInChannel(e.Series.Id, channel);
                if (check == false)
                    continue;
                await channel.Channel.SendMessageAsync(embed: builder.Build());
            }
        }

        private void SonarrWebhooksService_OnDownload(object sender, OnDownloadSonarrEvent e)
        {
            if (Episodes.TryGetValue(e.Series.Id, out var ep))
            {
                ep.Add(this, e).Wait();
            }
            else
            {
                var eps = new Episodes(this, e);
                Episodes[e.Series.Id] = eps;
#if DEBUG
                Trakt.CollectNew(eps).Wait();
#endif
            }

        }

        void loop(object param)
        {
            try
            {
                if (!(param is CancellationToken token))
                    return;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        Lock.WaitOne();
                        var rem = new List<int>();
                        foreach (var keypair in Episodes)
                        {
                            var value = keypair.Value;
                            var diff = DateTime.Now - value.Last;
                            if (diff.TotalMinutes >= 60)
                            {
                                rem.Add(keypair.Key);
                                Trakt.CollectNew(keypair.Value).Wait(token);
                            }
                        }
                        foreach (var x in rem)
                            Episodes.Remove(x);
                    }
                    finally
                    {
                        Lock.Release();
                    }
                    try
                    {
#if DEBUG
                        Task.Delay(Time.Ms.Minute * 2, token)
#else
                    Task.Delay(Time.Ms.Minute * 15, token)
#endif
                        .Wait();
                    }
                    catch (Exception ex)
                    {
                        Error(ex);
                        return;
                    }
                }
            } catch(TaskCanceledException e)
            {
                Error(e);
            } finally
            {
                Debug("Exited loop");
            }

        }

        public void SendError(Exception ex)
        {
            this.Error(ex);
        }

        public Dictionary<int, Episodes> Episodes { get; set; } = new Dictionary<int, Episodes>();
        public event EventHandler<OnTestSonarrEvent> OnTest;
        public event EventHandler<OnGrabSonarrEvent> OnGrab;
        public event EventHandler<OnDownloadSonarrEvent> OnDownload;
        public event EventHandler<OnEpisodeFileDeleteSonarrEvent> OnEpisodeFileDelete;

        public void Handle(SonarrEvent type)
        {
            Info($"Waiting lock for {type.EventType} | {typeof(SonarrEvent)}", "Handle");
            Lock.WaitOne();
            Info($"Received lock for {type.EventType}", "Handle");
            try
            {
                if (type is OnTestSonarrEvent t)
                    OnTest?.Invoke(this, t);
                else if (type is OnGrabSonarrEvent g)
                {
                    Info($"Invoking event for OnGrab", "OnGrab");
                    Info($"{OnGrab?.GetInvocationList().Length} listeners #4", "OnGrab");
                    OnGrab?.Invoke(this, g);
                }
                else if (type is OnDownloadSonarrEvent d)
                    OnDownload?.Invoke(this, d);
                else if (type is OnEpisodeFileDeleteSonarrEvent ep)
                    OnEpisodeFileDelete?.Invoke(this, ep);

            } finally
            {
                Lock.Release();
                Info($"Released lock for {type.EventType}", "OnGrab");
            }
        }
    }

    public class Save
    {
        public List<SaveChannel> Channels { get; set; } = new List<SaveChannel>();
    }
    public class SaveChannel
    {
        [JsonProperty("c")]
        public ITextChannel Channel { get; set; }
        [JsonProperty("p", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool ShowsPrivate { get; set; }

        [JsonProperty("t")]
        public List<string> TagRequired { get; set; } = new List<string>();
    }

    public class Episodes
    {
        public DateTime Last { get; set; }
        public SonarrStubSeries Series { get; set; }
        public List<SonarrEpisode> List { get; set; }
        public Dictionary<int, SonarrEpisodeFile> Files { get; set; }

        private Dictionary<ulong, IUserMessage> SaveMessages { get; set; } = new Dictionary<ulong, IUserMessage>();

        public Episodes(SonarrStubSeries series, IEnumerable<SonarrEpisode> info)
        {
            Files = new Dictionary<int, SonarrEpisodeFile>();
            Series = series;
            List = info.ToList();
            Last = DateTime.Now;
        }
        public Episodes(SonarrWebhooksService service, OnDownloadSonarrEvent evnt) : this(evnt.Series, new List<SonarrEpisode>())
        {
            Add(service, evnt).Wait();
        }
        public async Task Add(SonarrWebhooksService service, OnDownloadSonarrEvent evnt)
        {
            foreach (var x in evnt.Episodes)
                Add(x, evnt.EpisodeFile);
            await Submit(service);
        }
        void Add(SonarrEpisode ep, SonarrEpisodeFile file)
        {
            List.Add(ep);
            Files.Add(ep.Id, file);
        }
        public Embed ToEmbed()
        {
            var builder = new EmbedBuilder();
            builder.Color = Color.Green;
            builder.Title = List.Count == 1 ? "Episode Downloaded" : "Episodes Downloaded";
            builder.Description = $"{Series.Title}";
            builder.WithCurrentTimestamp();
            foreach (var episode in List)
            {
                var file = Files.GetValueOrDefault(episode.Id);
                builder.AddField($"S{episode.SeasonNumber:00}E{episode.EpisodeNumber:00}",
                    $"{episode.Title}\r\n{(file?.Quality ?? "n/a")}", true);
            }
            return builder.Build();
        }
    
    
        public async Task Submit(SonarrWebhooksService service)
        {
            var embed = this.ToEmbed();
            foreach (var txt in service.Channels)
            {
                var chnl = txt.Channel;
                if (chnl is NullTextChannel)
                {
                    continue;
                }
                var send = await service.ShouldSendInChannel(this.Series.Id, txt);
                if (send == false)
                    continue;
                try
                {
                    if (SaveMessages.TryGetValue(chnl.Id, out var msg))
                    {
                        await msg.ModifyAsync(x => x.Embed = embed);
                    }
                    else
                    {
                        var resp = await chnl.SendMessageAsync(embed: embed);
                        SaveMessages[chnl.Id] = resp;
                    }
                    Last = DateTime.Now;
                }
                catch (Exception ex)
                {
                    service.SendError(ex);
                }
            }
        }
    
    }

#region Events
    [JsonConverter(typeof(JsonSubtypes), "EventType")]
    [JsonSubtypes.KnownSubType(typeof(OnGrabSonarrEvent), "Grab")]
    [JsonSubtypes.KnownSubType(typeof(OnDownloadSonarrEvent), "Download")]
    [JsonSubtypes.KnownSubType(typeof(OnEpisodeFileDeleteSonarrEvent), "EpisodeFileDelete")]
    [JsonSubtypes.KnownSubType(typeof(OnTestSonarrEvent), "Test")]
    public abstract class SonarrEvent
    {
        public string EventType { get; set; }
        public SonarrStubSeries Series { get; set; }
    }

    public abstract class EpisodesSonarrEvent : SonarrEvent
    {
        public SonarrEpisode[] Episodes { get; set; }
    }

    public class OnGrabSonarrEvent : EpisodesSonarrEvent
    {
        public SonarrRelease Release { get; set; }
        public string DownloadClient { get; set; }
        public string DownloadId { get; set; }
    }

    public class OnDownloadSonarrEvent : EpisodesSonarrEvent
    {
        public SonarrEpisodeFile EpisodeFile { get; set; }
        public bool IsUpgrade { get; set; }
    }

    public class OnTestSonarrEvent : EpisodesSonarrEvent
    {
    }

    public class OnEpisodeFileDeleteSonarrEvent : EpisodesSonarrEvent
    {
        public SonarrEpisodeFile EpisodeFile { get; set; }
        public string DeleteReason { get; set; }
    }

    #endregion

    #region Infos
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class SonarrStubSeries
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public int TvDbId { get; set; }

        private string GetDebuggerDisplay()
        {
            return $"{Id} {Title} {TvDbId}";
        }
    }

    public class SonarrSeasonStats
    {
        public DateTimeOffset PreviousAiring { get; set; }
        public int EpisodeFileCount { get; set; }
        public int EpisodeCount { get; set; }
        public int TotalEpisodeCount { get; set; }
        public ulong SizeOnDisk { get; set; }
    }

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class SonarrSeasonStatus
    {
        public int SeasonNumber { get; set; }
        public bool Monitored { get; set; }
        public SonarrSeasonStats Statistics { get; set; }

        private string GetDebuggerDisplay()
            => $"S{SeasonNumber:00} {Statistics.EpisodeCount} / {Statistics.TotalEpisodeCount}";
    }
    public class SonarrImage
    {
        public string CoverType { get; set; }
        public string Url { get; set; }

        public string RemoteUrl { get; set; }
    }

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class SonarrSeries : SonarrStubSeries
    {
        public int SeasonCount { get; set; }
        public string Status { get; set; }
        public string Overview { get; set; }
        public string Network { get; set; }
        public string AirTime { get; set; }
        public SonarrImage[] Images { get; set; }
        public List<SonarrSeasonStatus> Seasons { get; set; }
        public int Year { get; set; }
        public bool SeasonFolder { get; set; }
        public bool Monitored { get; set; }
        public int Runtime { get; set; }
        public string[] Genres { get; set; }
        public string[] Tags { get; set; }

        public DateTimeOffset Added { get; set; }

        public int EpisodeCount { get; set; }
        public int TotalEpisodeCount { get; set; }

        private string GetDebuggerDisplay()
        {
            return $"{Id} {Title} seasons={SeasonCount}; eps={EpisodeCount} / {TotalEpisodeCount}";
        }
    }
    public class SonarrEpisode
    {
        public int Id { get; set; }
        public int EpisodeNumber { get; set; }
        public int SeasonNumber { get; set; }
        public string Title { get; set; }
        public DateTime? AirDate { get; set; }
        public DateTime? AirDateUtc { get; set; }
        public bool HasFile { get; set; }
    }
    public class SonarrRelease
    {
        public string ReleaseTitle { get; set; }
        public string Quality { get; set; }
        public int QualityVersion { get; set; }
        public long Size { get; set; }
    }
    public class SonarrEpisodeFile
    {
        public int Id { get; set; }
        public string RelativePath { get; set; }
        public string Path { get; set; }
        public string Quality { get; set; }
        public int QualityVersion { get; set; }
    }
    public class SonarrInnerQuality
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Source { get; set; }
        public int Resolution { get; set; }
    }
    public class SonarrRevision
    {
        public int Version { get; set; }
        public int Real { get; set; }
        public bool IsRepack { get; set; }
    }
    public class SonarrQuality
    {
        public SonarrInnerQuality Quality { get; set; }
        public SonarrRevision Proper { get; set; }
        public SonarrRevision Revision { get; set; }
    }

    public class SonarrHistoryCollection
    {
        [JsonProperty("page")]
        public int Page { get; set; }
        [JsonProperty("totalRecords")]
        public int TotalRecords { get; set; }
        [JsonProperty("records")]
        public SonarrHistoryRecord[] Records { get; set; }
    }
    [JsonConverter(typeof(JsonSubtypes), "EventType")]
    [JsonSubtypes.KnownSubType(typeof(SonarrHistoryGrabbedRecord), "grabbed")]
    [JsonSubtypes.FallBackSubType(typeof(SonarrHistoryGenericRecord))]
    public class SonarrHistoryRecord
    {
        public int episodeId { get; set; }
        public int seriesId { get; set; }
        public string SourceTitle { get; set; }
        public virtual string EventType { get; set; }
        public SonarrEpisode Episode { get; set; }
        public SonarrSeries Series { get; set; }
        public int Id { get; set; }
    }
    public class SonarrGrabbedData
    {
        public string indexer { get; set; }
        public string releaseGroup { get; set; }
        public string nzbInfoUrl { get; set; }
        /// <summary>
        /// Whole number of days
        /// </summary>
        public int age { get; set; }
        public double ageHours { get; set; }
        public double ageMinutes { get; set; }
        public string downloadUrl { get; set; }
        public string guid { get; set; }
    }
    public class SonarrHistoryGrabbedRecord : SonarrHistoryRecord
    {
        public override string EventType => "grabbed";
        public SonarrGrabbedData data { get; set; }
    }
    public class SonarrHistoryGenericRecord : SonarrHistoryRecord
    {
        public JObject data { get; set; }
    }

#endregion
}
