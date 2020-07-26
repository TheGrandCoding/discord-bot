using Discord;
using DiscordBot.Classes.Converters;
using DiscordBot.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace DiscordBot.Classes.Chess.COA
{
    public class CoAHearing
    {
        [JsonConstructor]
        private CoAHearing(int? claimant = null, int? respondent = null, string Verdict = null, DateTime? Opened = null,
            int? casenumber = null)
        {
            Claimants = new List<ChessPlayer>();
            if (claimant.HasValue)
                Claimants.Add(ChessService.Players.FirstOrDefault(x => x.Id == claimant));
            Respondents = new List<ChessPlayer>();
            if (respondent.HasValue)
                Respondents.Add(ChessService.Players.FirstOrDefault(x => x.Id == respondent));
            CaseNumber = casenumber ?? 0; 
            Holding = Verdict;
            if(Opened.HasValue)
            {
                Filed = Opened.Value;
                Commenced = Filed;
                Concluded = Filed;
            }

        }
        public CoAHearing(IEnumerable<ChessPlayer> claimants, IEnumerable<ChessPlayer> respondents)
        {
            Claimants = claimants.ToList();
            Respondents = respondents.ToList();
        }
        
        public List<ChessPlayer> Claimants { get; set; }

        public List<ChessPlayer> Respondents { get; set; }

        public List<ChessPlayer> Justices { get; set; }

        [JsonIgnore]
        public string Title => string.Join("; ", Claimants.Select(x => x.Name)) + " v. " + string.Join("; ", Respondents.Select(x => x.Name));
        [JsonProperty("cn")]
        public int CaseNumber { get; set; }
        [JsonProperty("sl")]
        public bool Sealed { get; set; }

        public DateTime Filed { get; set; }
        public DateTime? Commenced { get; set; }
        public DateTime? Concluded { get; set; }

        public List<CoAMotion> Motions { get; set; } = new List<CoAMotion>();
        public List<CoAWitness> Witnesses { get; set; } = new List<CoAWitness>();
        public List<CoARuling> Rulings { get; set; } = new List<CoARuling>();

        public string Holding { get; set; }

        [JsonIgnore]
        public string DataPath => System.IO.Path.Combine(Program.BASE_PATH, "data", "coa", CaseNumber.ToString("0000"));

        public void SetIds()
        {
            foreach (var m in Motions)
                m.SetIds(this);
            foreach (var w in Witnesses)
                w.SetIds(this);
            foreach (var r in Rulings)
                r.SetIds(this);
        }

        public bool CanCallWitness(ChessPlayer player)
        {
            return Claimants.Any(x => x.Id == player.Id)
                || Respondents.Any(x => x.Id == player.Id)
                || Justices.Any(x => x.Id == player.Id);
        }

    }
}
