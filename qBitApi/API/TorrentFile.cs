using System;
using System.Collections.Generic;
using System.Text;

namespace qBitApi.API
{
    internal class TorrentFile
    {
        public string name { get; set; }
        public long size { get; set; }
        public float progress { get; set; }
        public FilePriority priority { get; set; }
        public bool is_seed { get; set; }
        public long[] piece_range { get; set; }
        public float availability { get; set; }
    }
}
