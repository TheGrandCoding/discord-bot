using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.CoronAPI
{
    public struct TimelineStats
    {
        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }
        [JsonProperty("date")]
        public DateTime Date { get; set; }
        [JsonProperty("deaths")]
        public int? Deaths { get; set; }
        [JsonProperty("confirmed")]
        public int? Confirmed { get; set; }
        [JsonProperty("recovered")]
        public int? Recovered { get; set; }
        [JsonProperty("active")]
        public int? Active { get; set; }
        [JsonProperty("new_confirmed")]
        public int? NewConfirmed { get; set; }
        [JsonProperty("new_recovered")]
        public int? NewRecovered { get; set; }
        [JsonProperty("new_deaths")]
        public int? NewDeaths { get; set; }
        [JsonProperty("is_in_progress")]
        public bool InProgress { get; set; }
    }
}
