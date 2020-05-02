using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.CoronAPI
{
    public class CoronaResponse
    {
        [JsonProperty("data")]
        public CoronaData Data { get; set; }
        [JsonProperty("_cacheHit")]
        public bool Cache { get; set; }
    }
}
