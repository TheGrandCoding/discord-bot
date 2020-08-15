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
    public class AppealHearing
    {
        [JsonConstructor]
        private AppealHearing(int? claimant = null, int? respondent = null, string Verdict = null, DateTime? Opened = null,
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
        public AppealHearing(IEnumerable<ChessPlayer> claimants, IEnumerable<ChessPlayer> respondents)
        {
            Claimants = claimants.ToList();
            Respondents = respondents.ToList();
        }
        
        public List<ChessPlayer> Claimants { get; set; }

        public List<ChessPlayer> Respondents { get; set; }

        public List<ChessPlayer> Justices { get; set; }

        public bool isJudgeOnCase(ChessPlayer player)
        {
            if(IsArbiterCase)
            {
                return player.Permission.HasFlag(ChessPerm.Arbiter);
            } else
            {
                return (Justices != null && Justices.Contains(player)) || (Justices == null && player.Permission.HasFlag(ChessPerm.Justice));
            }
        }
        public bool isClerkOnCase(ChessPlayer player)
        {
            if (IsArbiterCase)
                return player.Permission.HasFlag(ChessPerm.Arbiter);
            return player.Permission.HasFlag(ChessPerm.ChiefJustice);
        }

        public string getRelationToCase(ChessPlayer player)
        {
            if (Claimants.Contains(player))
                return "Claimant";
            if (Respondents.Contains(player))
                return "Respondent";
            if(IsArbiterCase)
            {
                if (player.Permission == ChessPerm.Arbiter)
                    return "Arbiter";
            } else
            {
                if (isJudgeOnCase(player))
                    return "Justice";
            }
            return "Outsider";
        }

        [JsonIgnore]
        public string Title => string.Join("; ", Claimants.Select(x => x.Name)) + " v. " + string.Join("; ", Respondents.Select(x => x.Name));
        [JsonProperty("cn")]
        public int CaseNumber { get; set; }
        [JsonProperty("sl")]
        public bool Sealed { get; set; }

        [JsonProperty("arb")]
        public bool IsArbiterCase { get; set; }

        [JsonProperty("apcn", NullValueHandling = NullValueHandling.Ignore)]
        public int? AppealOf { get; set; }

        public DateTime Filed { get; set; }
        public DateTime? Commenced { get; set; }
        public DateTime? Concluded { get; set; }

        public List<CoAMotion> Motions { get; set; } = new List<CoAMotion>();
        public List<CoAWitness> Witnesses { get; set; } = new List<CoAWitness>();
        public CoARuling Ruling { get; set; }

        public string Holding { get; set; }

        [JsonIgnore]
        public string DataPath => System.IO.Path.Combine(Program.BASE_PATH, "data", "coa", CaseNumber.ToString("0000"));

        public void SetIds()
        {
            foreach (var m in Motions)
                m.SetIds(this);
            foreach (var w in Witnesses)
                w.SetIds(this);
            Ruling?.SetIds(this);
        }

        public bool CanCallWitness(ChessPlayer player)
        {
            return Claimants.Any(x => x.Id == player.Id)
                || Respondents.Any(x => x.Id == player.Id)
                || isJudgeOnCase(player);
        }

    }
}
