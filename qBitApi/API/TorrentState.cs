using System;
using System.Collections.Generic;
using System.Text;

namespace qBitApi
{
    public enum TorrentState
    {
        Unknown,
        /// <summary>
        /// Some error occurred, applies to paused torrents
        /// </summary>
        Error,
        /// <summary>
        /// Torrent data files is missing
        /// </summary>
        MissingFiles,
        /// <summary>
        /// Torrent is being seeded and data is being transferred
        /// </summary>
        Uploading,
        /// <summary>
        /// Torrent is paused and has finished downloading
        /// </summary>
        PausedUP,
        /// <summary>
        /// Queuing is enabled and torrent is queued for upload
        /// </summary>
        QueuedUP,
        /// <summary>
        /// Torrent is being seeded, but no connection were made
        /// </summary>
        StalledUP,
        /// <summary>
        /// Torrent has finished downloading and is being checked
        /// </summary>
        CheckingUP,
        /// <summary>
        /// Torrent is forced to uploading and ignore queue limit
        /// </summary>
        ForcedUP,
        /// <summary>
        /// Torrent is allocating disk space for download
        /// </summary>
        Allocating,
        /// <summary>
        /// Torrent is being downloaded and data is being transferred
        /// </summary>
        Downloading,
        /// <summary>
        /// Torrent has just started downloading and is fetching metadata
        /// </summary>
        MetaDL,
        /// <summary>
        /// Torrent is paused and has NOT finished downloading
        /// </summary>
        PausedDL,
        /// <summary>
        /// Queuing is enabled and torrent is queued for download
        /// </summary>
        QueuedDL,
        /// <summary>
        /// Torrent is being downloaded, but no connection were made
        /// </summary>
        StalledDL,
        /// <summary>
        /// Same as checkingUP, but torrent has NOT finished downloading
        /// </summary>
        CheckingDL,
        /// <summary>
        /// Torrent is forced to downloading to ignore queue limit
        /// </summary>
        ForceDL,
        /// <summary>
        /// Checking resume data on qBt startup
        /// </summary>
        CheckingResumeData,
        /// <summary>
        /// Torrent is moving to another location
        /// </summary>
        Moving
    }
}
