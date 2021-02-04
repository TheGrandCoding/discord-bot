using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Model = qBitApi.API.TorrentFile;

namespace qBitApi.REST.Entities
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class TorrentFile
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public float Progress { get; set; }
        public FilePriority Priority { get; set; }
        public bool IsSeed { get; set; }
        public long[] PieceRange { get; set; }
        public float Availability { get; set; }

        private string DebuggerDisplay { get
            {
                return $"{Name} {Size}b {(Progress * 100):00}%";
            } }

        internal TorrentFile() { }

        internal void Update(Model model)
        {
            Name = model.name;
            Size = model.size;
            Progress = model.progress;
            Priority = model.priority;
            IsSeed = model.is_seed;
            PieceRange = model.piece_range;
            Availability = model.availability;
        }

        internal static TorrentFile Create(Model model)
        {
            var f = new TorrentFile();
            f.Update(model);
            return f;
        }
    }
}
