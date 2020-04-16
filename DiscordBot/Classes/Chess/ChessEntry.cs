using Newtonsoft.Json;
using System.ComponentModel;
using System.Diagnostics;

namespace DiscordBot.Classes.Chess
{
    [DebuggerDisplay("{againstId,nq} {State,nq}")]
    public class ChessEntry
    {
        [JsonIgnore]
        static uint _id = 1;
        [JsonIgnore]
        public uint Id { get; set; }

        [JsonConstructor]
        public ChessEntry()
        {
            Id = _id++;
        }

        [JsonProperty("against")]
        public int againstId { get; set; }
        public ChessGameStatus State { get; set; }
        [JsonIgnore]
        public int selfWas { get; set; }
        [JsonIgnore]
        public int otherWas { get; set; }
        [JsonProperty("online", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool onlineGame { get; set; } = false;
    }
}
