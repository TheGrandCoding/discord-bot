using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.CoronAPI
{
    public struct CoronaData
    {
        [JsonProperty("coordinates")]
        public Coordinates Coordinates { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("code")]
        public string Code { get; set; }
        [JsonProperty("population")]
        public int Population { get; set; }
        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }
        [JsonProperty("today")]
        public TodayStats Today { get; set; }
        [JsonProperty("latest_data")]
        public LatestStats Latest { get; set; }
        [JsonProperty("timeline")]
        public TimelineStats[] Timeline { get; set; }
    }
}
