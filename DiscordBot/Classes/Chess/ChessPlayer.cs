using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.Chess
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class ChessPlayer
    {
        public ChessPlayer()
        {
            Id = ++Services.ChessService.PlayerIdMax;
        }

        [JsonConstructor]
        private ChessPlayer(int id)
        {
            Id = id;
            if (Services.ChessService.PlayerIdMax < id)
                Services.ChessService.PlayerIdMax = id;
        }

        [JsonProperty]
        public int Id { get; set; }

        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty("perm", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(ChessPerm.Player)]
        public ChessPerm Permission { get; set; } = ChessPerm.Player;

        [JsonProperty("rem", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Removed { get; set; } = false;

        [JsonProperty("reqt", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool RequireTiming { get; set; } = false;

        [JsonIgnore]
        public string DebuggerDisplay => $"{Id} {Name} {ConnectedAccount} {Permission}";

        [JsonProperty("ds")]
        public ulong ConnectedAccount { get; set; }

        [JsonProperty("last", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? DateLastPresent { get; set; } = null;

        [JsonProperty("bu")]
        [DefaultValue(false)]
        public bool IsBuiltInAccount { get; set; } = false;

        /// <summary>
        /// If this Member has been dismissed from office by the CoA, what reason. Null if not dismissed.
        /// </summary>
        [JsonProperty("dism", NullValueHandling = NullValueHandling.Ignore)]
        public string DismissalReason { get; set; }

        public List<ChessDay> Days { get; set; } = new List<ChessDay>();

        [JsonProperty("score")]
        int rating = 100;

        [JsonProperty("score_mod")]
        int modifier = 0;

        public void SetScoreOnDay(int score, DateTime day)
        {
            var thing = Days.FirstOrDefault(x => x.Date.DayOfYear == day.DayOfYear && x.Date.Year == day.Year);
            if(thing == null)
            {
                thing = new ChessDay();
                thing.Date = day;
                Days.Add(thing);
            }
            thing.Score = score;
        }

        public int GetScoreOnDay(DateTime day)
        {
            var thing = Days.FirstOrDefault(x => x.Date.DayOfYear == day.DayOfYear && x.Date.Year == day.Year);
            if(thing == null)
                return 0;
            return thing.Score;
        }

        public ChessEntry SetGameOnDay(ChessPlayer other, ChessGameStatus state, DateTime day)
        {
            var thing = Days.FirstOrDefault(x => x.Date.DayOfYear == day.DayOfYear && x.Date.Year == day.Year);
            if (thing == null)
            {
                thing = new ChessDay();
                thing.Date = day;
                Days.Add(thing);
            }
            var entry = new ChessEntry()
            {
                againstId = other.Id,
                State = state
            };
            thing.Entries.Add(entry);
            return entry;
        }

        public void SetRating(int score, BotUser by, string reason)
        {
            if(rating != score)
            {
                int diff = score - rating;
                rating = score;
                var note = new ChessNote(by, $"Set score to {score} ({(diff > 0 ? $"+{diff}" : $"{diff}")}) for {reason}", 10);
                Notes.Add(note);
            }
        }

        [JsonIgnore]
        public int Rating
        {
            get
            {
                return rating;
            } set
            {
                if(value < 100)
                {
                    rating = 100;
                } else
                {
                    rating = value;
                    SetScoreOnDay(value, DateTime.Now);
                }
            }
        }

        [JsonIgnore]
        public int Modifier
        {
            get
            {
                return modifier;
            } set
            {
                modifier = value;
            }
        }

        [JsonProperty("w", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int Wins { get; set; }
        [JsonProperty("l", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int Losses { get; set; }

        [JsonIgnore]
        public double WinRate {  get
            {
                double total = Wins + Losses;
                if (total == 0)
                    return 0;
                return (double)Wins / total;
            } }

        public List<ChessNote> Notes { get; set; } = new List<ChessNote>();

        [JsonIgnore]
        public List<ChessNote> ActiveNotes => Notes.Where(x => !x.Expired).ToList();

        [JsonProperty("monitor", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool RequireGameApproval { get; set; } = false;

        [JsonProperty("bans")]
        public List<ChessBan> Bans { get; set; } = new List<ChessBan>();

        // some helper properties
        [JsonIgnore]
        public ChessBan FirstActiveBan => Bans.FirstOrDefault(x => x.IsActive);
        [JsonIgnore]
        public bool IsBanned => FirstActiveBan != null;

        [JsonIgnore]
        public bool ShouldContinueInLoop => this.Removed || this.IsBuiltInAccount;

        [JsonIgnore]
        public string VerifyOnlineReference { get; set; }

        [JsonIgnore]
        public List<int> OnlineGamesPlayedAgainst { get; set; } = new List<int>();

        [JsonProperty("elect")]
        public Dictionary<int, int> ArbiterVotePreferences { get; set; } = new Dictionary<int, int>();

        [JsonProperty("noelect")]
        public bool WithdrawnModVote { get; set; } = false;
    }
}
