using System;
using System.Collections.Generic;
using System.Text;
using Model = qBitApi.API.BuildInfo;

namespace qBitApi.REST.Entities
{
    public class BuildInfo
    {
        public string qT { get;  }
        public string LibTorrent { get; }
        public string Boost { get; }
        public string OpenSSL { get; }
        public int Bitness { get; }

        internal BuildInfo(string qt, string lib, string bst, string open, int bit)
        {
            qT = qt;
            LibTorrent = lib;
            Boost = bst;
            OpenSSL = open;
            Bitness = bit;
        }

        internal static BuildInfo Create(Model model)
        {
            return new BuildInfo(model.qt, model.libtorrent, model.boost, model.openssl, model.bitness);
        }
    }
}
