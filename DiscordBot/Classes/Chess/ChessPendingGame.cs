using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.Chess
{
    public class ChessPendingGame
    {
        public string Reference { get
            {
                return $"{Player1Id}{Player2Id}{RecordedAt.DayOfYear}{(int)RecordedAt.TimeOfDay.TotalMinutes}";
            } }
        [JsonConstructor]
        private ChessPendingGame(int player1id, int player2id)
        {
            _player1Id = player1id;
            _player2Id = player2id;
        }
        public ChessPendingGame(ChessPlayer p1, ChessPlayer p2, DateTime time, int p1Change, int p2Change)
        {
            Player1 = p1;
            Player2 = p2;
            RecordedAt = time;
            P1_StartScore = p1.Rating; // since they havne't had score changed yet
            P2_StartScore = p2.Rating;
            P1_Change = p1Change;
            P2_Change = p2Change;
        }
        [JsonIgnore]
        public ChessPlayer Player1;
        private int _player1Id;
        [JsonProperty]
        public int Player1Id => Player1?.Id ?? _player1Id;

        [JsonProperty("online")]
        public bool OnlineGame { get; set; }

        [JsonIgnore]
        public ChessPlayer Player2;
        private int _player2Id;
        [JsonProperty]
        public int Player2Id => Player2?.Id ?? _player2Id;

        [JsonProperty("p1s")]
        public int P1_StartScore;
        [JsonProperty("p2s")]
        public int P2_StartScore;

        [JsonProperty("p1c")]
        public int P1_Change;
        [JsonProperty("p2c")]
        public int P2_Change;

        [JsonProperty("d")]
        public bool Draw;

        [JsonProperty("time")]
        public DateTime RecordedAt;

        public void SetIds()
        {
            Player1 = Services.ChessService.Players.FirstOrDefault(x => x.Id == Player1Id);
            Player2 = Services.ChessService.Players.FirstOrDefault(x => x.Id == Player2Id);
        }
    }
}
