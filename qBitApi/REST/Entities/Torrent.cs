using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading.Tasks;
using Model = qBitApi.API.ListTorrentInfo;

namespace qBitApi.REST.Entities
{
    public class Torrent
    {
        /// <summary>
        /// Torrent hash
        /// </summary>
        public string Hash { get; private set; }
        public string Name { get; private set; }

        /// <summary>
        /// Time (Unix Epoch) when the torrent was added to the client
        /// </summary>
        public DateTimeOffset AddedOn { get; private set; }
        /// <summary>
        /// Amount of data left to download (bytes)
        /// </summary>
        public long AmountLeft { get; private set; }
        /// <summary>
        /// Whether this torrent is managed by Automatic Torrent Management
        /// </summary>
        public bool AutomaticTorrentManagement { get; private set; }
        /// <summary>
        /// Percentage of file pieces currently available
        /// </summary>
        public float Availability { get; private set; }
        /// <summary>
        /// Category of the torrent
        /// </summary>
        public string Category { get; private set; }
        /// <summary>
        /// Amount of transfer data completed (bytes)
        /// </summary>
        public long Completed { get; private set; }
        /// <summary>
        /// Time when the torrent completed
        /// </summary>
        public DateTimeOffset CompletionOn { get; private set; }
        /// <summary>
        /// Absolute path of torrent content (root path for multifile torrents, absolute file path for singlefile torrents)
        /// </summary>
        public string ContentPath { get; private set; }
        /// <summary>
        /// Torrent download speed limit (bytes/s). -1 if ulimited.
        /// </summary>
        public long DownloadLimit { get; private set; }
        /// <summary>
        /// Torrent download speed (bytes/s)
        /// </summary>
        public long DownloadSpeed { get; private set; }
        public long Downloaded { get; private set; }
        public long DownloadedSession { get; private set; }
        public TimeSpan ETA { get; private set; }
        public bool FirstLastPiecePriority { get; private set; }
        public bool ForceStart { get; private set; }
        public DateTimeOffset LastActivity { get; private set; }
        public string MagnetUri { get; private set; }
        public float MaxRatio { get; private set; }
        public int MaxSeedingTime { get; private set; }
        public int SwarmSeeds { get; private set; }
        public int SwarmLeeches { get; private set; }
        public int ConnectedSeeds { get; private set; }
        public int ConnectedLeeches { get; private set; }
        public int Priority { get; private set; }
        public float Progress { get; private set; }
        public float Ratio { get; private set; }
        public float RatioLimit { get; private set; }
        public string SavePath { get; private set; }
        public int SeedingTimeLimit { get; private set; }
        public DateTimeOffset SeenComplete { get; private set; }
        public bool SequentialDownload { get; private set; }
        /// <summary>
        /// Total size (bytes) of files selected for download
        /// </summary>
        public long Size { get; private set; }
        public TorrentState State { get; private set; }
        public bool SuperSeeding { get; private set; }
        public IReadOnlyList<string> Tags { get; private set; }
        public TimeSpan TimeActive { get; private set; }
        /// <summary>
        /// Total size (bytes) of all file in this torrent (including unselected ones)
        /// </summary>
        public long TotalSize { get; private set; }
        public string Tracker { get; private set; }
        public long UpLimit { get; private set; }
        public long Uploaded { get; private set; }
        public long UploadedSession { get; private set; }
        public long UploadSpeed { get; private set; }

        internal Torrent()
        {
        }

        internal void Update(Model model)
        {
            AddedOn = model.added_on.GetValueOrDefault(AddedOn);
            AmountLeft = model.amount_left.GetValueOrDefault(AmountLeft);
            AutomaticTorrentManagement = model.auto_tmm.GetValueOrDefault(AutomaticTorrentManagement);
            Availability = model.availability.GetValueOrDefault(Availability);
            Category = model.category.GetValueOrDefault(Category);
            Completed = model.completed.GetValueOrDefault(Completed);
            CompletionOn = model.completion_on.GetValueOrDefault(CompletionOn);
            ContentPath = model.content_path.GetValueOrDefault(ContentPath);
            DownloadLimit = model.dl_limit.GetValueOrDefault(DownloadLimit);
            DownloadSpeed = model.dlspeed.GetValueOrDefault(DownloadSpeed);
            Downloaded = model.downloaded.GetValueOrDefault(Downloaded);
            DownloadedSession = model.downloaded_session.GetValueOrDefault(DownloadedSession);
            if (model.eta.IsSpecified)
                ETA = TimeSpan.FromSeconds(model.eta.Value);
            FirstLastPiecePriority = model.f_l_piece_prio.GetValueOrDefault(FirstLastPiecePriority);
            ForceStart = model.force_start.GetValueOrDefault(ForceStart);
            Hash = model.hash.GetValueOrDefault(Hash);
            LastActivity = model.last_activity.GetValueOrDefault(LastActivity);
            MagnetUri = model.magnet_uri.GetValueOrDefault(MagnetUri);
            MaxRatio = model.max_ratio.GetValueOrDefault(MaxRatio);
            MaxSeedingTime = model.max_seeding_time.GetValueOrDefault(MaxSeedingTime);
            Name = model.name.GetValueOrDefault(Name);
            SwarmSeeds = model.num_complete.GetValueOrDefault(SwarmSeeds);
            SwarmLeeches = model.num_incomplete.GetValueOrDefault(SwarmLeeches);
            ConnectedLeeches = model.num_leechs.GetValueOrDefault(ConnectedLeeches);
            ConnectedSeeds = model.num_seeds.GetValueOrDefault(ConnectedSeeds);
            Priority = model.priority.GetValueOrDefault(Priority);
            Progress = model.progress.GetValueOrDefault(Progress);
            Ratio = model.ratio.GetValueOrDefault(Ratio);
            RatioLimit = model.ratio_limit.GetValueOrDefault(RatioLimit);
            SavePath = model.save_path.GetValueOrDefault(SavePath);
            SeedingTimeLimit = model.seeding_time_limit.GetValueOrDefault(SeedingTimeLimit);
            SeenComplete = model.seen_complete.GetValueOrDefault(SeenComplete);
            SequentialDownload = model.seq_dl.GetValueOrDefault(SequentialDownload);
            Size = model.size.GetValueOrDefault(Size);
            if (model.state.IsSpecified)
                State = Enum.Parse<TorrentState>(model.state.Value);
            SuperSeeding = model.super_seeding.GetValueOrDefault(SuperSeeding);
            if (model.tags.IsSpecified)
                Tags = model.tags.Value.Split(",").ToImmutableArray();
            if (model.time_active.IsSpecified)
                TimeActive = TimeSpan.FromSeconds(model.time_active.Value);
            TotalSize = model.total_size.GetValueOrDefault(TotalSize);
            Tracker = model.tracker.GetValueOrDefault(Tracker);
            UpLimit = model.up_limit.GetValueOrDefault(UpLimit);
            Uploaded = model.uploaded.GetValueOrDefault(Uploaded);
            UploadedSession = model.uploaded_session.GetValueOrDefault(UploadedSession);
            UploadSpeed = model.upspeed.GetValueOrDefault(UploadSpeed);
        }
    }
}
