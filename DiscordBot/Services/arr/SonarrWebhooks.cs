using Discord;
using DiscordBot.Classes;
using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
            foreach(var chnl in save.Channels)
            {
                if (!(chnl.Channel is NullTextChannel))
                    Channels.Add(chnl);
            }
        }

        public override void OnLoaded()
        {
            HTTP = new HttpClient();
            var apiKey = Program.Configuration["tokens:sonarr"];
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException("Sonarr API Key missing at tokens:sonarr");
            HTTP.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            var th = new Thread(loop);
            th.Start(Program.GetToken());
            OnDownload += SonarrWebhooksService_OnDownload;
            OnGrab += SonarrWebhooksService_OnGrab;
#if DEBUG
            OnTest += (object sender, OnTestSonarrEvent e) =>
            {
                Episodes[e.Series.Id] = new Episodes(e.Series, e.Episodes);
            };
#endif
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

        public async Task<string[]> GetSeriesTags(int seriesId)
        {
            if (SeriesTagsCache.TryGetValue(seriesId, out var v))
                return v;
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl + $"/series/{seriesId}");
            var response = await HTTP.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            Program.LogMsg($"{seriesId} :: {response.StatusCode}", LogSeverity.Info, "GetSeriesTags");
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
            Program.LogMsg($"For {seriesId}: [{string.Join(", ", parsed)}]", LogSeverity.Info, "GetSeriesTags");
            return parsed.ToArray();
        }

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
            Program.LogMsg($"Starting handler", Discord.LogSeverity.Info, "OnGrab");
            try
            {
                HandleOnGrabAsync(e).Wait();
            }
            catch (Exception ex)
            {
                Program.LogMsg(ex, "OnGrab:" + e.DownloadId ?? "");
            }
            Program.LogMsg($"Finished handler", Discord.LogSeverity.Info, "OnGrab");
        }

        public async Task<SonarrHistoryGrabbedRecord> GetHistory(int episodeId, int seriesId, int attempts = 0)
        {
            var url = apiUrl + $"/history?sortKey=date&episodeId={episodeId}";
            Program.LogMsg($"Sending GET{attempts} {url}", LogSeverity.Info, "OnGrab");
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await HTTP.SendAsync(request);
            Program.LogMsg($"{episodeId} :: {response.StatusCode}");
            var content = await response.Content.ReadAsStringAsync();
            Program.LogMsg($"Parsed content to string", Discord.LogSeverity.Info, "OnGrab");
            var history = JsonConvert.DeserializeObject<SonarrHistoryCollection>(content);
            Program.LogMsg($"Got {history.Records.Length}/{history.TotalRecords} records", Discord.LogSeverity.Info, "OnGrab");

            var recentGrab = history.Records
                .Where(x => x.Series.Id == seriesId)
                .FirstOrDefault(x => x is SonarrHistoryGrabbedRecord) as SonarrHistoryGrabbedRecord;
            if (recentGrab != null)
                return recentGrab;
            Program.LogMsg($"Failed to get history for {episodeId}, waiting then retrying");
            if(attempts > 10)
            {
                Program.LogMsg("Too many attempts, exiting");
                return null;
            }
            Thread.Sleep(1000 * attempts);
            return await GetHistory(episodeId, seriesId, attempts + 1);
        }

        public async Task HandleOnGrabAsync(OnGrabSonarrEvent e)
        {
            Program.LogMsg($"Handling {e.Series.Title}", LogSeverity.Info, "OnGrab");
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
                        ?? recentGrab.Series.Images.First()).Url;
                }
                Program.LogMsg($"{episode.Id} :: {recentGrab.Series.Title} {recentGrab.data.guid}");
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
                                var send = ShouldSendInChannel(keypair.Value.Series.Id, txt).Result;
                                if (send == false)
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

        [JsonProperty("t")]
        public List<string> TagRequired { get; set; } = new List<string>();
    }

    public class Episodes
    {
        public DateTime Last { get; set; }
        public SonarrStubSeries Series { get; set; }
        public List<SonarrEpisode> List { get; set; }
        public Dictionary<int, SonarrEpisodeFile> Files { get; set; }
        public Episodes(SonarrStubSeries series, IEnumerable<SonarrEpisode> info)
        {
            Files = new Dictionary<int, SonarrEpisodeFile>();
            Series = series;
            List = info.ToList();
            Last = DateTime.Now;
        }
        public Episodes(OnDownloadSonarrEvent evnt) : this(evnt.Series, new List<SonarrEpisode>())
        {
            Add(evnt);
        }
        public void Add(OnDownloadSonarrEvent evnt)
        {
            foreach (var x in evnt.Episodes)
                Add(x, evnt.EpisodeFile);
        }
        public void Add(SonarrEpisode ep, SonarrEpisodeFile file)
        {
            List.Add(ep);
            Files.Add(ep.Id, file);
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
                var file = Files.GetValueOrDefault(episode.Id);
                builder.AddField($"S{episode.SeasonNumber:00}E{episode.EpisodeNumber:00}",
                    $"{episode.Title}\r\n{(file?.Quality ?? "n/a")}", true);
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

#endregion

#region Infos
    public class SonarrStubSeries
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public int TvDbId { get; set; }
    }
    public class SonarrSeasonStatus
    {
        public int SeasonNumber { get; set; }
        public bool Monitored { get; set; }
    }
    public class SonarrImage
    {
        public string CoverType { get; set; }
        public string Url { get; set; }
    }
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
    }
    public class SonarrEpisode
    {
        public int Id { get; set; }
        public int EpisodeNumber { get; set; }
        public int SeasonNumber { get; set; }
        public string Title { get; set; }
        public DateTime? AirDate { get; set; }
        public DateTime? AirDateUtc { get; set; }
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
