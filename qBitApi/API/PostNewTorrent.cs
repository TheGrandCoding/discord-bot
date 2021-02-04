using qBitApi.REST.Net;
using System;
using System.Collections.Generic;
using System.Text;

namespace qBitApi.API
{
    public class PostNewTorrent
    {
        /// <summary>
        /// URLs or magnet URIs linking to the torrent
        /// <para>
        /// At least one of <see cref="Urls"/> or <see cref="TorrentFilePath"/> must be present
        /// </para>
        /// </summary>
        public string[] Urls { get; set; }
        /// <summary>
        /// Path to .torrent file
        /// <para>
        /// At least one of <see cref="Urls"/> or <see cref="TorrentFilePath"/> must be present
        /// </para>
        /// </summary>
        public string TorrentFilePath { get; set; }
        /// <summary>
        /// Download folder
        /// </summary>
        public Optional<string> SavePath { get; set; }
        /// <summary>
        /// Cookie sent to download the .torrent file
        /// </summary>
        public Optional<string> Cookie { get; set; }
        /// <summary>
        /// Category for the torrent
        /// </summary>
        public Optional<string> Category { get; set; }
        /// <summary>
        /// Tags for the torrent
        /// </summary>
        public Optional<string[]> Tags { get; set; }
        /// <summary>
        /// Skip hash checking
        /// </summary>
        public Optional<bool> SkipCheck { get; set; }
        /// <summary>
        /// Add torrents in the paused state
        /// </summary>
        public Optional<bool> Paused { get; set; }
        /// <summary>
        /// Create the root folder
        /// </summary>
        public Optional<bool> RootFolder { get; set; }
        /// <summary>
        /// Rename the torrent to specific value
        /// </summary>
        public Optional<string> Rename { get; set; }
        /// <summary>
        /// Set torrent upload speed, bytes/second
        /// </summary>
        public Optional<long> UploadLimit { get; set; }
        /// <summary>
        /// Set torrent download speed limit, bytes/second
        /// </summary>
        public Optional<long> DownloadLimit { get; set; }
        /// <summary>
        /// Add torrent in Automatic Torrent Management
        /// </summary>
        public Optional<bool> Automatic { get; set; }
    }
}
