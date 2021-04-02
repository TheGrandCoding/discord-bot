using Discord;
using DiscordBot.Classes;
using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services.Sonarr
{
    public class SonarrWebhooksService : SavedService
    {
        public List<SaveChannel> Channels { get; set; }

#if DEBUG
        const string apiUrl = "http://192.168.1.3:8989/api";
#else
        const string apiUrl = "http://localhost:8989/api";
#endif

        public Semaphore Lock = new Semaphore(1, 1);
        public HttpClient HTTP { get; private set; }
        public CacheDictionary<int, string[]> TagsCache { get; } = new CacheDictionary<int, string[]>(60 * 24); // day

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
            Channels = save.Channels ?? new List<SaveChannel>();
        }

        public override void OnLoaded()
        {
            HTTP = new HttpClient();
            var apiKey = Program.Configuration["tokens:sonarr"];
            Program.LogMsg($"X-API-Key is {(apiKey == null ? "null" : "not null")}", Discord.LogSeverity.Info, "OnGrab");
            HTTP.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            var th = new Thread(loop);
            th.Start(Program.GetToken());
            OnDownload += SonarrWebhooksService_OnDownload;
            Program.LogMsg($"{OnGrab?.GetInvocationList().Length} listeners #1", LogSeverity.Info, "OnGrab");
            OnGrab += SonarrWebhooksService_OnGrab;
            Program.LogMsg($"{OnGrab?.GetInvocationList().Length} listeners #2", LogSeverity.Info, "OnGrab");
            OnGrab += (object sender, OnGrabSonarrEvent e) =>
            {
                Program.LogMsg($"Invoked bracket");
            };
            Program.LogMsg($"{OnGrab?.GetInvocationList().Length} listeners #3", LogSeverity.Info, "OnGrab");
#if DEBUG
            OnTest += (object sender, OnTestSonarrEvent e) =>
            {
                Episodes[e.Series.Id] = new Episodes(e.Series, e.Episodes);
            };
#endif
        }

        public async Task<string[]> GetSeriesTags(int seriesId)
        {
            if (TagsCache.TryGetValue(seriesId, out var v))
                return v;
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl + $"/series/{seriesId}");
            var response = await HTTP.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            Program.LogMsg($"{seriesId} :: {response.StatusCode}", LogSeverity.Info, "GetSeriesTags");
            var jobj = JObject.Parse(content);
            var array = jobj["tags"].ToObject<string[]>();
            TagsCache.Add(seriesId, array);
            return array;
        }

        private void SonarrWebhooksService_OnGrab(object sender, OnGrabSonarrEvent e)
        {
            Program.LogMsg($"Starting handler", Discord.LogSeverity.Info, "OnGrab");
            try
            {
                HandleOnGrabAsync(e).Wait();
            }
            catch (Exception ex)
            {
                Program.LogMsg(ex, "OnGrab");
            }
            Program.LogMsg($"Finished handler", Discord.LogSeverity.Info, "OnGrab");
        }

        public async Task HandleOnGrabAsync(OnGrabSonarrEvent e)
        {
            Program.LogMsg($"Handling {e.Series.Title}", LogSeverity.Info, "OnGrab");
            //var episodes = new Dictionary<int, bool>();
            var releases = new List<GrabbedData>();
            bool isPrivate = false;
            var builder = new EmbedBuilder();
            builder.Title = "Episodes Grabbed";
            builder.Description = $"{e.Series.Title}; {e.Episodes.Length} episodes found";
            foreach (var episode in e.Episodes)
            {
                //if (episodes.ContainsKey(episode.Id))
                //    continue;
                var url = apiUrl + $"/history?epsiodeId={episode.Id}";
                Program.LogMsg($"Sending GET {url}", LogSeverity.Info, "OnGrab");
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                var response = await HTTP.SendAsync(request);
                Program.LogMsg($"{e.Series.Title} {episode.SeasonNumber}{episode.EpisodeNumber} {episode.Id} :: {response.StatusCode}");
                var content = await response.Content.ReadAsStringAsync();
                Program.LogMsg($"Parsed content to string", Discord.LogSeverity.Info, "OnGrab");
                var history = JsonConvert.DeserializeObject<HistoryCollection>(content);
                Program.LogMsg($"Got {history.TotalRecords} records", Discord.LogSeverity.Info, "OnGrab");
                var recentGrab = history.Records.FirstOrDefault(x => x is HistoryGrabbedRecord) as HistoryGrabbedRecord;
                TagsCache[e.Series.Id] = recentGrab.Series.Tags;
                isPrivate = recentGrab.Series.Tags.Contains("private");
                //episodes[episode.Id] = true;
                var existing = releases.Any(x => x.guid == recentGrab.data.guid);
                if(!existing)
                    releases.Add(recentGrab.data);
                if(builder.Fields.Count < 23)
                    builder.AddField($"S{episode.SeasonNumber:00}E{episode.EpisodeNumber:00}", episode.Title + "\r\n" + episode.Quality, true);
            }
            var relStr = "";
            foreach(var rel in releases)
            {
                var time = Program.FormatTimeSpan(TimeSpan.FromMinutes(rel.ageMinutes), true);
                relStr += $"[From {rel.indexer} by {rel.releaseGroup} {time}]({rel.nzbInfoUrl})\r\n";
            }
            builder.AddField("Release" + (releases.Count > 1 ? "s" : ""), relStr, false);
            foreach (var channel in Channels) {
                if (channel.Channel is NullTextChannel)
                    continue;
                if (isPrivate && !channel.ShowsPrivate)
                    continue;
                await channel.Channel.SendMessageAsync(embed: builder.Build());
            }
        }

        private void SonarrWebhooksService_OnDownload(object sender, OnDownloadSonarrEvent e)
        {
            if (Episodes.TryGetValue(e.Series.Id, out var ep))
                ep.Add(e);
            else
                Episodes[e.Series.Id] = new Episodes(e);
        }

        void loop(object param)
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
                        if (diff.TotalMinutes >= 15)
                        {
                            var isprivate = GetSeriesTags(keypair.Value.Series.Id).Result.Contains("private");
                            var embed = value.ToEmbed();
                            var toRemove = new List<ITextChannel>();
                            foreach (var txt in Channels)
                            {
                                var chnl = txt.Channel;
                                if (chnl is NullTextChannel)
                                {
                                    toRemove.Add(chnl);
                                    continue;
                                }
                                if (isprivate && txt.ShowsPrivate == false)
                                    continue;
                                try
                                {
                                    chnl.SendMessageAsync(embed: embed);
                                } catch (Exception ex)
                                {
                                    toRemove.Add(chnl);
                                    Program.LogMsg("Sonarr", ex);
                                }
                            }
                            rem.Add(keypair.Key);
                        }
                    }
                    foreach (var x in rem)
                        Episodes.Remove(x);
                } finally
                {
                    Lock.Release();
                }
                try
                {
#if DEBUG
                    Task.Delay(Time.Minute * 2, token)
#else
                    Task.Delay(Time.Minute * 15, token)
#endif
                        .Wait();
                }
                catch (Exception ex)
                {
                    Program.LogMsg("SonarrLoop", ex);
                    return;
                }
            }
            Program.LogMsg("Exited loop", LogSeverity.Debug, "SonarWebhooks");
        }


        public Dictionary<int, Episodes> Episodes { get; set; } = new Dictionary<int, Episodes>();
        public event EventHandler<OnTestSonarrEvent> OnTest;
        public event EventHandler<OnGrabSonarrEvent> OnGrab;
        public event EventHandler<OnDownloadSonarrEvent> OnDownload;

        public void Handle(SonarrEvent type)
        {
            Program.LogMsg($"Waiting lock for {type.EventType} | {typeof(SonarrEvent)}", Discord.LogSeverity.Info, "OnGrab");
            Lock.WaitOne();
            Program.LogMsg($"Received lock for {type.EventType}", Discord.LogSeverity.Info, "OnGrab");
            try
            {
                if (type is OnTestSonarrEvent t)
                    OnTest?.Invoke(this, t);
                else if (type is OnGrabSonarrEvent g)
                {
                    Program.LogMsg($"Invoking event for OnGrab", Discord.LogSeverity.Info, "OnGrab");
                    Program.LogMsg($"{OnGrab?.GetInvocationList().Length} listeners #4", LogSeverity.Info, "OnGrab");
                    OnGrab?.Invoke(this, g);
                }
                else if (type is OnDownloadSonarrEvent d)
                    OnDownload?.Invoke(this, d);
            } finally
            {
                Lock.Release();
            Program.LogMsg($"Released lock for {type.EventType}", Discord.LogSeverity.Info, "OnGrab");
            }
        }
    }

    public class Save
    {
        public List<SaveChannel> Channels { get; set; }
    }
    public class SaveChannel
    {
        [JsonProperty("c")]
        public ITextChannel Channel { get; set; }
        [JsonProperty("p", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool ShowsPrivate { get; set; }
    }

    public class Episodes
    {
        public DateTime Last { get; set; }
        public StubSeriesInfo Series { get; set; }
        public List<EpisodeInfo> List { get; set; }
        public Episodes(StubSeriesInfo series, IEnumerable<EpisodeInfo> info)
        {
            Series = series;
            List = info.ToList();
            Last = DateTime.Now;
        }
        public Episodes(OnDownloadSonarrEvent evnt)
        {
            List = new List<EpisodeInfo>();
            Series = evnt.Series;
            Add(evnt);
        }
        public void Add(OnDownloadSonarrEvent evnt)
        {
            foreach (var x in evnt.Episodes)
                Add(x);
        }
        public void Add(EpisodeInfo ep)
        {
            List.Add(ep);
            Last = DateTime.Now;
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
                builder.AddField($"S{episode.SeasonNumber:00}E{episode.EpisodeNumber:00}",
                    $"{episode.Title}, {episode.Quality}", true);
            }
            return builder.Build();
        }
    }

#region Events
    [JsonConverter(typeof(JsonSubtypes), "EventType")]
    [JsonSubtypes.KnownSubType(typeof(OnGrabSonarrEvent), "Grab")]
    [JsonSubtypes.KnownSubType(typeof(OnDownloadSonarrEvent), "Download")]
    [JsonSubtypes.KnownSubType(typeof(OnTestSonarrEvent), "Test")]
    public abstract class SonarrEvent
    {
        public string EventType { get; set; }
        public StubSeriesInfo Series { get; set; }
    }

    public abstract class EpisodesSonarrEvent : SonarrEvent
    {
        public EpisodeInfo[] Episodes { get; set; }
    }

    public class OnGrabSonarrEvent : EpisodesSonarrEvent
    {
        public ReleaseInfo Release { get; set; }
        public string DownloadClient { get; set; }
        public string DownloadId { get; set; }
    }

    public class OnDownloadSonarrEvent : EpisodesSonarrEvent
    {
        public EpisodeFileInfo EpisodeFile { get; set; }
        public bool IsUpgrade { get; set; }
    }

    public class OnTestSonarrEvent : EpisodesSonarrEvent
    {
    }

#endregion

#region Infos
    public class StubSeriesInfo
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public int TvDbId { get; set; }
    }
    public class SeasonStatus
    {
        public int SeasonNumber { get; set; }
        public bool Monitored { get; set; }
    }
    public class FullSeriesInfo : StubSeriesInfo
    {
        public int SeasonCount { get; set; }
        public string Status { get; set; }
        public string Overview { get; set; }
        public string Network { get; set; }
        public string AirTime { get; set; }
        public JArray Images { get; set; }
        public List<SeasonStatus> Seasons { get; set; }
        public int Year { get; set; }
        public bool SeasonFolder { get; set; }
        public bool Monitored { get; set; }
        public int Runtime { get; set; }
        public string[] Genres { get; set; }
        public string[] Tags { get; set; }
    }
    public class EpisodeInfo
    {
        public int Id { get; set; }
        public int EpisodeNumber { get; set; }
        public int SeasonNumber { get; set; }
        public string Title { get; set; }
        public DateTime? AirDate { get; set; }
        public DateTime? AirDateUtc { get; set; }
        public string Quality { get; set; }
        public int QualityVersion { get; set; }
    }
    public class ReleaseInfo
    {
        public string Quality { get; set; }
        public int QualityVersion { get; set; }
        public long Size { get; set; }
    }
    public class EpisodeFileInfo
    {
        public int Id { get; set; }
        public string RelativePath { get; set; }
        public string Path { get; set; }
        public string Quality { get; set; }
        public int QualityVersion { get; set; }
    }
    public class QualityInfo2
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Source { get; set; }
        public int Resolution { get; set; }
    }
    public class RevisionInfo
    {
        public int Version { get; set; }
        public int Real { get; set; }
        public bool IsRepack { get; set; }
    }
    public class QualityInfo
    {
        public QualityInfo2 Quality { get; set; }
        public RevisionInfo Proper { get; set; }
    }

    public class HistoryCollection
    {
        [JsonProperty("page")]
        public int Page { get; set; }
        [JsonProperty("totalRecords")]
        public int TotalRecords { get; set; }
        [JsonProperty("records")]
        public HistoryRecord[] Records { get; set; }
    }
    [JsonConverter(typeof(JsonSubtypes), "EventType")]
    [JsonSubtypes.KnownSubType(typeof(HistoryGrabbedRecord), "grabbed")]
    [JsonSubtypes.FallBackSubType(typeof(HistoryGenericRecord))]
    public class HistoryRecord
    {
        public int episodeId { get; set; }
        public int seriesId { get; set; }
        public string SourceTitle { get; set; }
        public virtual string EventType { get; set; }
        public EpisodeInfo Episode { get; set; }
        public FullSeriesInfo Series { get; set; }
        public int Id { get; set; }
    }
    public class GrabbedData
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
    public class HistoryGrabbedRecord : HistoryRecord
    {
        public override string EventType => "grabbed";
        public GrabbedData data { get; set; }
    }
    public class HistoryGenericRecord : HistoryRecord
    {
        public JObject data { get; set; }
    }

#endregion
}
