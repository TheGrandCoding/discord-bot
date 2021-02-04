using System;
using System.Collections.Generic;
using System.Text;

namespace qBitApi.API
{
    internal class BuildInfo
    {
        public string qt { get; }
        public string libtorrent { get; }
        public string boost { get; }
        public string openssl { get; }
        public int bitness { get; }
    }
}
