using System;
using System.Collections.Generic;
using System.Text;

namespace qBitApi.API
{
    internal class CommonTorrentInfo
    {
        public Optional<string> save_path { get; set; }
        public Optional<long> up_limit { get; set; }
        public Optional<long> dl_limit { get; set; }
    }
    internal class GenericTorrentInfo : CommonTorrentInfo
    {
        [UnixTimestamp]
        public Optional<DateTimeOffset> creation_date { get; set; }

        public Optional<long> piece_size { get; set; }
        public Optional<string> comment { get; set; }
        public Optional<long> total_wasted { get; set; }
        public Optional<long> total_uploaded { get; set; }
        public Optional<long> total_uploaded_session { get; set; }
        public Optional<long> total_downloaded { get; set; }
        public Optional<long> total_downloaded_session { get; set; }
    }
    internal class ListTorrentInfo : CommonTorrentInfo
    {
        [UnixTimestamp]
        public Optional<DateTimeOffset> added_on { get; set; }
        public Optional<long> amount_left { get; set; }
        public Optional<bool> auto_tmm { get; set; }
        public Optional<float> availability { get; set; }
        public Optional<string> category { get; set; }
        public Optional<long> completed { get; set; }
        [UnixTimestamp]
        public Optional<DateTimeOffset> completion_on { get; set; }
        public Optional<string> content_path { get; set; }
        public Optional<long> dlspeed { get; set; }
        public Optional<long> downloaded { get; set; }
        public Optional<long> downloaded_session { get; set; }
        public Optional<long> eta { get; set; }
        public Optional<bool> f_l_piece_prio { get; set; }
        public Optional<bool> force_start { get; set; }
        public Optional<string> hash { get; set; }
        [UnixTimestamp]
        public Optional<DateTimeOffset> last_activity { get; set; }
        public Optional<string> magnet_uri { get; set; }
        public Optional<float> max_ratio { get; set; }
        public Optional<int> max_seeding_time { get; set; }
        public Optional<string> name { get; set; }
        public Optional<int> num_complete { get; set; }
        public Optional<int> num_incomplete { get; set; }
        public Optional<int> num_leechs { get; set; }
        public Optional<int> num_seeds { get; set; }
        public Optional<int> priority { get; set; }
        public Optional<float> progress { get; set; }
        public Optional<float> ratio { get; set; }
        public Optional<float> ratio_limit { get; set; }
        public Optional<int> seeding_time_limit { get; set; }
        [UnixTimestamp]
        public Optional<DateTimeOffset> seen_complete { get; set; }
        public Optional<bool> seq_dl { get; set; }
        public Optional<long> size { get; set; }
        public Optional<string> state { get; set; }
        public Optional<bool> super_seeding { get; set; }
        public Optional<string> tags { get; set; }
        public Optional<int> time_active { get; set; }
        public Optional<long> total_size { get; set; }
        public Optional<string> tracker { get; set; }
        public Optional<long> uploaded { get; set; }
        public Optional<long> uploaded_session { get; set; }
        public Optional<long> upspeed { get; set; }
    }

    

}
