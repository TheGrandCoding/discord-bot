using Discord;
using DiscordBot.Classes;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services.Sonarr
{
    public class SonarrWebhooksService : SavedService
    {
        public List<ITextChannel> Channels { get; set; }

        public Semaphore Lock = new Semaphore(1, 1);

        public override string GenerateSave()
        {
            return Program.Serialise(Channels);
        }
        public override void OnReady()
        {
            var sv = ReadSave("[]");
            Channels = Program.Deserialise<List<ITextChannel>>(sv) ?? new List<ITextChannel>();
        }

        public override void OnLoaded()
        {
            var th = new Thread(loop);
            th.Start(Program.GetToken());
            OnDownload += SonarrWebhooksService_OnDownload;
#if DEBUG
            OnTest += (object sender, OnTestSonarrEvent e) =>
            {
                Episodes[e.Series.Id] = new Episodes(e.Series, e.Episodes);
            };
#endif
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
            while(!token.IsCancellationRequested)
            {
                try
                {
                    Lock.WaitOne();
                    var rem = new List<int>();
                    foreach(var keypair in Episodes)
                    {
                        var value = keypair.Value;
                        var diff = DateTime.Now - value.Last;
                        if(diff.TotalMinutes >= 15)
                        {
                            var embed = value.ToEmbed();
                            var toRemove = new List<ITextChannel>();
                            foreach(var chnl in Channels)
                            {
                                if(chnl is NullTextChannel)
                                {
                                    toRemove.Add(chnl);
                                    continue;
                                }
                                try
                                {
                                    chnl.SendMessageAsync(embed: embed);
                                } catch(Exception ex)
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

        public SonarrEvent parse(JObject jobject)
        {
            var type = jobject["eventType"].ToObject<string>();
            if (type == "Test")
                return new OnTestSonarrEvent(jobject);
            if (type == "Grab")
                return new OnGrabSonarrEvent(jobject);
            if (type == "Download")
                return new OnDownloadSonarrEvent(jobject);
            throw new InvalidOperationException($"Cannot handle event of type '{type}'");
        }

        public void Handle(JObject jobject)
        {
            Lock.WaitOne();
            try
            {
                var type = parse(jobject);
                if (type is OnTestSonarrEvent t)
                    OnTest?.Invoke(this, t);
                else if (type is OnGrabSonarrEvent g)
                    OnGrab?.Invoke(this, g);
                else if (type is OnDownloadSonarrEvent d)
                    OnDownload?.Invoke(this, d);
            } finally
            {
                Lock.Release();
            }
        }
    }

    public class Episodes
    {
        public DateTime Last { get; set; }
        public SeriesInfo Series { get; set; }
        public List<EpisodeInfo> List { get; set; }
        public Episodes(SeriesInfo series, IEnumerable<EpisodeInfo> info)
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
            foreach(var episode in List)
            {
                builder.AddField($"S{episode.SeasonNumber:00}E{episode.EpisodeNumber:00}",
                    $"{episode.Title}, {episode.Quality}", true);
            }
            return builder.Build();
        }
    }

    #region Events
    public abstract class SonarrEvent
    {
        public string EventType { get; }
        public SeriesInfo Series { get; }
        public SonarrEvent(JObject jobj)
        {
            EventType = jobj["eventType"].ToObject<string>();
            Series = new SeriesInfo(jobj["series"] as JObject);
        }
    }

    public abstract class EpisodesSonarrEvent : SonarrEvent
    {
        public EpisodeInfo[] Episodes { get; }
        public EpisodesSonarrEvent(JObject jobj) : base(jobj)
        {
            var jarray = jobj["episodes"] as JArray;
            Episodes = jarray
                .Select(x => new EpisodeInfo(x as JObject))
                .ToArray();
        }
    }

    public class OnGrabSonarrEvent : EpisodesSonarrEvent
    {
        public ReleaseInfo Release { get; }
        public OnGrabSonarrEvent(JObject jobj) : base(jobj)
        {
            Release = new ReleaseInfo(jobj["release"] as JObject);
        }
    }

    public class OnDownloadSonarrEvent : EpisodesSonarrEvent
    {
        public EpisodeFileInfo EpisodeFile { get; }
        public bool IsUpgrade { get; }
        public OnDownloadSonarrEvent(JObject jobj) : base(jobj)
        {
            EpisodeFile = new EpisodeFileInfo(jobj["episodeFile"] as JObject);
            IsUpgrade = jobj["isUpgrade"].ToObject<bool>();
        }
    }

    public class OnTestSonarrEvent : EpisodesSonarrEvent
    {
        public OnTestSonarrEvent(JObject jobj) : base(jobj)
        {
        }
    }

    #endregion

    #region Infos
    public class SeriesInfo
    {
        public int Id { get; }
        public string Title { get; }
        public string Path { get; }
        public int TvDbId { get; }
        public SeriesInfo(JObject jobj)
        {
            Id = jobj["id"].ToObject<int>();
            Title = jobj["title"].ToObject<string>();
            Path = jobj["path"].ToObject<string>();
            TvDbId = jobj["tvdbId"].ToObject<int>();
        }
    }
    public class EpisodeInfo
    {
        public int Id { get; }
        public int EpisodeNumber { get; }
        public int SeasonNumber { get; }
        public string Title { get; }
        public DateTime? AirDate { get; }
        public DateTime? AirDateUtc { get; }
        public string Quality { get; }
        public int QualityVersion { get; }
        public EpisodeInfo(JObject jobj)
        {
            Id = jobj["id"].ToObject<int>();
            EpisodeNumber = jobj["episodeNumber"].ToObject<int>();
            SeasonNumber = jobj["seasonNumber"].ToObject<int>();
            Title = jobj["title"].ToObject<string>();
            if (jobj.TryGetValue("airDate", out var ad))
                AirDate = ad.ToObject<DateTime>();
            if (jobj.TryGetValue("airDateUtc", out var adu))
                AirDateUtc = adu.ToObject<DateTime>();
            Quality = jobj["quality"]?.ToObject<string>() ?? null;
            QualityVersion = jobj["qualityVersion"]?.ToObject<int>() ?? 0;
        }
    }
    public class ReleaseInfo
    {
        public string Quality { get; }
        public int QualityVersion { get; }
        public long Size { get; }
        public ReleaseInfo(JObject jobj)
        {
            Quality = jobj["quality"]?.ToObject<string>() ?? "";
            QualityVersion = jobj["qualityVersion"].ToObject<int>();
            Size = jobj["size"]?.ToObject<long>() ?? 0;
        }
    }
    public class EpisodeFileInfo
    {
        public int Id { get; }
        public string RelativePath { get; }
        public string Path { get; }
        public string Quality { get; }
        public int QualityVersion { get; }
        public EpisodeFileInfo(JObject jobj)
        {
            Id = jobj["id"].ToObject<int>();
            RelativePath = jobj["relativePath"].ToObject<string>();
            Path = jobj["path"].ToObject<string>();
            Quality = jobj["quality"]?.ToObject<string>() ?? "";
            QualityVersion = jobj["qualityVersion"]?.ToObject<int>() ?? 0;
        }
    }
    #endregion
}
