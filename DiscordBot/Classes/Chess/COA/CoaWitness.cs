using DiscordBot.Classes.Converters;
using DiscordBot.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Chess.COA
{
    public class CoAWitness
    {
        public CoAWitness(ChessPlayer witness)
        {
            Witness = witness;
        }

        [JsonProperty("witnessId")]
        [JsonConverter(typeof(ChessPlayerConverter))]
        public ChessPlayer Witness { get; set; }

        public DateTime? ConcludedOn { get; set; }

        [JsonIgnore]
        public CoAHearing Hearing { get; set; }

        public void SetIds(CoAHearing hearing)
        {
            Hearing = hearing;
        }
    }
}
