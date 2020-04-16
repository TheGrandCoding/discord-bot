using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.Chess
{
    public class ChessBan
    {
        [JsonConstructor]
        private ChessBan(int givenbyid)
        {
            _givenById = givenbyid;
        }
        public ChessBan(ChessPlayer against, ChessPlayer by)
        {
            Against = against;
            GivenBy = by;
        }
        [JsonIgnore]
        public ChessPlayer GivenBy { get; set; }
        private int _givenById;
        [JsonProperty("by")]
        public int GivenById => GivenBy?.Id ?? _givenById;

        [JsonIgnore]
        public ChessPlayer Against { get; private set; }

        [JsonProperty("r")]
        public string Reason { get; set; }

        [JsonProperty("at")]
        public DateTime GivenAt { get; set; }

        [JsonProperty("exp")]
        public DateTime ExpiresAt { get; set; }

        [JsonIgnore]
        public bool IsActive => ExpiresAt > DateTime.Now;

        public void SetIds(ChessPlayer against)
        {
            Against = against;
            GivenBy = Services.ChessService.Players.FirstOrDefault(x => x.Id == GivenById);
        }
    }
}
