using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DiscordBot.Classes.Chess
{
    [DebuggerDisplay("{Date,nq} {Score,nq} {Entries.Count}")]
    public class ChessDay
    {
        public ChessDay() { }

        public DateTime Date { get; set; } = DateTime.Now;
        public int Score { get; set; }
        public List<ChessEntry> Entries { get; set; } = new List<ChessEntry>();

        [JsonConstructor]
        private ChessDay(DateTime date, List<ChessEntry> entries)
        {
            Date = date;
            Entries = entries;
        }
    }
}
