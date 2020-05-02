using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.CoronAPI
{
    public struct TodayStats
    {
        [JsonProperty("deaths")]
        public int? Deaths { get; set; }
        [JsonProperty("confirmed")]
        public int? Confirmed { get; set; }
    }
}
