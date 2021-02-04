using System;
using System.Collections.Generic;
using System.Text;

namespace qBitApi.API
{
    internal class SyncInfo
    {
        public int rid { get; set; }
        public bool full_update { get; set; }
        public Dictionary<string, ListTorrentInfo> torrents { get; set; }
        public string[] torrents_removed { get; set; }
        public object categories { get; set; }
        public string[] categories_removed { get; set; }
        public string[] tags { get; set; }
        public string[] tags_removed { get; set; }
        public object server_state { get; set; }
    }
}
