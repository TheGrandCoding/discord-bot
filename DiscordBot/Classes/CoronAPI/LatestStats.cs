using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.CoronAPI
{
    public struct LatestStats
    {
        [JsonProperty("deaths")]
        public int? Deaths { get; set; }
        [JsonProperty("confirmed")]
        public int? Confirmed { get; set; }
        [JsonProperty("recovered")]
        public int? Recovered { get; set; }
        [JsonProperty("critical")]
        public int? Critical { get; set; }
    }

    public struct LatestCalcStats
    {
        [JsonProperty("death_rate")]
        public float? DeathRate { get; set; }
        [JsonProperty("recovery_rate")]
        public float? RecoveryRate { get; set; }
        [JsonProperty("reovered_vs_death_ratio")]
        public float? RecoveredVsDeathRatio { get; set; }
        [JsonProperty("cases_per_million_population")]
        public float? CasesPerMillion { get; set; }
    }
}
