using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.Chess.CoA
{
    public class CoAHearing
    {
        public static int MaxId = 0;
        public CoAHearing(ChessPlayer plain, ChessPlayer def, string desc)
        {
            Plaintiff = plain;
            Defendant = def;
            Description = desc;
            Opened = DateTime.Now;
            this.CaseNumber = ++MaxId;
        }
        [JsonIgnore]
        public string Title => $"{Plaintiff.Name} v. {Defendant.Name}";
        [JsonProperty]
        public string Verdict { get; set; } = "";
        [JsonIgnore]
        public bool HasFinished => !string.IsNullOrWhiteSpace(Verdict);
        [JsonIgnore]
        public ChessPlayer Plaintiff;
        [JsonProperty]
        int plainId => Plaintiff?.Id ?? _tempPlainId;
        int _tempPlainId;
        [JsonIgnore]
        public ChessPlayer Defendant;
        [JsonProperty]
        int defendId => Defendant?.Id ?? _tempDefendId;
        int _tempDefendId;

        [JsonIgnore]
        public CoAHearing AppealHearing { get; set; }
        [JsonProperty]
        int appealNumber => AppealHearing?.CaseNumber ?? _tempAppealNumber;
        int _tempAppealNumber;
        [JsonIgnore]
        public CoAHearing AnAppealOf { get; set; }

        [JsonProperty("isappeal")]
        public bool IsAppealRequested { get; set; }

        public int JudgeRulingMinimum { get
            {
                switch(Justices.Length)
                {
                    case 0:
                        return 0;
                    case 1:
                        return 1;
                    case 2:
                    case 3:
                        return 2;
                    case 4:
                    case 5:
                        return 3;
                    case 6:
                    case 7:
                        return 4;
                    case 8:
                    case 9:
                        return 5;
                    default:
                        return (int)Math.Round(Justices.Length / (double)2);
                }
            } }

        [JsonIgnore]
        public ChessPlayer[] Justices = new ChessPlayer[0];
        [JsonProperty]
        int[] justicesId {  get
            {
                if(Justices == null || Justices.Length == 0)
                {
                    return _tempJusticesId ?? new int[0];
                }
                return Justices.Select(x => x.Id).ToArray();
            } }
        int[] _tempJusticesId;

        [JsonProperty("overt")]
        public List<int> JusticesRuleOverturn { get; set; } = new List<int>();
        [JsonProperty("dism")]
        public List<int> JusticesRuleDismiss { get; set; } = new List<int>();
        [JsonProperty("uphol")]
        public List<int> JusticesRuleUpheld { get; set; } = new List<int>();
        [JsonProperty("remand")]
        public List<int> JusticesRuleRemand { get; set; } = new List<int>();
        [JsonProperty("jother")]
        public List<int> JusticesRuleOther { get; set; } = new List<int>();

        public bool HasJusticeVoted(ChessPlayer player)
        {
            List<int> possibilities = new List<int>();
            possibilities.AddRange(JusticesRuleOverturn);
            possibilities.AddRange(JusticesRuleDismiss);
            possibilities.AddRange(JusticesRuleUpheld);
            possibilities.AddRange(JusticesRuleRemand);
            possibilities.AddRange(JusticesRuleOther);
            foreach (var x in possibilities)
                if (x == player.Id)
                    return true;
            return false;
        }
        public string GetJusticeVote(ChessPlayer player)
        {
            if (JusticesRuleDismiss.Contains(player.Id))
                return "Dismiss";
            if (JusticesRuleOther.Contains(player.Id))
                return "Other";
            if (JusticesRuleOverturn.Contains(player.Id))
                return "Overturn";
            if (JusticesRuleRemand.Contains(player.Id))
                return "Remand";
            if (JusticesRuleUpheld.Contains(player.Id))
                return "Uphold";
            return "Undecided";
        }
        public bool AddVote(ChessPlayer player, string type)
        {
            if (!Justices.Select(x => x.Id).Contains(player.Id))
                return false;
            if (type == "Dismiss")
                JusticesRuleDismiss.Add(player.Id);
            else if (type == "Other")
                JusticesRuleOther.Add(player.Id);
            else if (type == "Overturn")
                JusticesRuleOverturn.Add(player.Id);
            else if (type == "Remand")
                JusticesRuleRemand.Add(player.Id);
            else if (type == "Uphold")
                JusticesRuleUpheld.Add(player.Id);
            else
                return false;
            return true;

        }
        public List<ChessPlayer> GetVoteCount(Predicate<string> func)
        {
            List<int> c = new List<int>();
            if (func("Dismiss"))
                foreach (var u in JusticesRuleDismiss)
                    c.Add(u);
            if (func("Other"))
                foreach (var u in JusticesRuleOther)
                    c.Add(u);
            if (func("Overturn"))
                foreach (var u in JusticesRuleOverturn)
                    c.Add(u);
            if (func("Remand"))
                foreach (var u in JusticesRuleRemand)
                    c.Add(u);
            if (func("Uphold"))
                foreach (var u in JusticesRuleUpheld)
                    c.Add(u);
            return c.Select(x => Justices.FirstOrDefault(y => y.Id == x)).ToList();
        }
        public bool HasReachedMajority(out string type, out List<ChessPlayer> justices)
        {
            justices = new List<ChessPlayer>();
            type = "";
            if (GetVoteCount(x => true).Count < Justices.Length)
                return false; // cant reach a verdict since not all judges have voted
            if(JusticesRuleDismiss.Count >= JudgeRulingMinimum)
            {
                justices = JusticesRuleDismiss.Select(x => Justices.FirstOrDefault(y => y.Id == x)).ToList();
                type = "Dismiss";
                return true;
            }
            if (JusticesRuleOther.Count >= JudgeRulingMinimum)
            {
                justices = JusticesRuleOther.Select(x => Justices.FirstOrDefault(y => y.Id == x)).ToList();
                type = "Other";
                return true;
            }
            if (JusticesRuleOverturn.Count >= JudgeRulingMinimum)
            {
                justices = JusticesRuleOverturn.Select(x => Justices.FirstOrDefault(y => y.Id == x)).ToList();
                type = "Overturn";
                return true;
            }
            if (JusticesRuleRemand.Count >= JudgeRulingMinimum)
            {
                justices = JusticesRuleRemand.Select(x => Justices.FirstOrDefault(y => y.Id == x)).ToList();
                type = "Remand";
                return true;
            }
            if (JusticesRuleUpheld.Count >= JudgeRulingMinimum)
            {
                justices = JusticesRuleUpheld.Select(x => Justices.FirstOrDefault(y => y.Id == x)).ToList();
                type = "Uphold";
                return true;
            }
            return false;
        }
        public int RemoveJusticeVotes(ChessPlayer player)
        {
            int total = 0;
            total += JusticesRuleDismiss.RemoveAll(x => x == player.Id);
            total += JusticesRuleOther.RemoveAll(x => x == player.Id);
            total += JusticesRuleOverturn.RemoveAll(x => x == player.Id);
            total += JusticesRuleRemand.RemoveAll(x => x == player.Id);
            total += JusticesRuleUpheld.RemoveAll(x => x == player.Id);
            return total;
        }

        [JsonIgnore]
        public ICategoryChannel Category;
        [JsonProperty]
        ulong categoryId => Category?.Id ?? _tempCategoryId;
        ulong _tempCategoryId;

        [JsonIgnore]
        public ITextChannel GeneralChnl;
        [JsonProperty]
        ulong generalId => GeneralChnl?.Id ?? _tempGeneralId; 
        ulong _tempGeneralId;

        [JsonIgnore]
        public ITextChannel JusticesChnl;
        [JsonProperty]
        ulong justicesChnlId => JusticesChnl?.Id ?? _tempJusticesChnlId;
        ulong _tempJusticesChnlId;

        [JsonIgnore]
        public bool IsRequested => Category == null;

        public int CaseNumber { get; set; } = 0;

        [JsonIgnore]
        public string CaseStr => CaseNumber.ToString("0000");

        public string Description { get; set; }

        public DateTime Opened { get; set; }

        public bool CanCallWitnesses(ChessPlayer player)
        {
            if (player == null)
                return false;
            var ids = new List<int>
            {
                Plaintiff.Id,  Defendant.Id
            };
            ids.AddRange(Justices.Select(x => x.Id));
            return ids.Contains(player.Id);
        }

        public void ClearChannelPermissions()
        {
            foreach(var chnl in new ITextChannel[] { GeneralChnl, JusticesChnl})
            {
                foreach(var over in chnl.PermissionOverwrites)
                {
                    if(over.TargetType == PermissionTarget.Role)
                    {
                        var r = Program.ChessGuild.GetRole(over.TargetId);
                        chnl.RemovePermissionOverwriteAsync(r);
                    } else
                    {
                        var u = Program.ChessGuild.GetRole(over.TargetId);
                        chnl.RemovePermissionOverwriteAsync(u);
                    }
                }
            }
        }

        public void SetChannelPermissions()
        {
            if (GeneralChnl == null)
                return;
            GeneralChnl.AddPermissionOverwriteAsync(Program.ChessGuild.EveryoneRole, Program.NoPerms);
            GeneralChnl.AddPermissionOverwriteAsync(MLAPI.Modules.CoA.Member, Program.ReadPerms);
            var allStanding = new List<ChessPlayer>() { Plaintiff, Defendant };
            allStanding.AddRange(Justices);
            foreach(var p in allStanding)
            {
                var usr = Program.ChessGuild.GetUser(p.ConnectedAccount);
                if (usr != null)
                {
                    GeneralChnl.AddPermissionOverwriteAsync(usr, HasFinished ? Program.ReadPerms : Program.WritePerms);
                }
            }
            JusticesChnl.AddPermissionOverwriteAsync(Program.ChessGuild.EveryoneRole, Program.NoPerms);
            foreach(var p in Justices)
            {
                var usr = Program.ChessGuild.GetUser(p.ConnectedAccount);
                if(usr != null)
                {
                    JusticesChnl.AddPermissionOverwriteAsync(usr, HasFinished ? Program.ReadPerms : Program.WritePerms);
                }
            }
            foreach (var wit in Witnesses)
                wit.SetPermissions();
            if(HasFinished)
            {
                JusticesChnl.AddPermissionOverwriteAsync(MLAPI.Modules.CoA.ChiefJustice, Program.ReadPerms);
            }
        }

        [JsonConstructor]
        private CoAHearing(int plainId, int defendId, int[] justicesId, ulong categoryId, int casenumber, ulong justiceschnlid, ulong generalid)
        {
            CaseNumber = casenumber;
            if (casenumber > MaxId)
                MaxId = casenumber;
            _tempPlainId = plainId;
            _tempDefendId = defendId;
            _tempJusticesId = justicesId;
            _tempCategoryId = categoryId;
            _tempJusticesChnlId = justiceschnlid;
            _tempGeneralId = generalid;
        }

        public void SetIds()
        {
            Plaintiff = Services.ChessService.Players.FirstOrDefault(x => x.Id == plainId);
            Defendant = Services.ChessService.Players.FirstOrDefault(x => x.Id == defendId);
            if(justicesId.Length > 0)
            {
                Justices = justicesId.Select(x => Services.ChessService.Players.FirstOrDefault(y => y.Id == x)).ToArray();
            } else
            {
                Justices = new ChessPlayer[0];
            }
            if (categoryId != 0)
            {
                Category = Program.ChessGuild.GetCategoryChannel(categoryId);
                GeneralChnl = Program.ChessGuild.GetTextChannel(generalId);
                JusticesChnl = Program.ChessGuild.GetTextChannel(justicesChnlId);
            }
            foreach (var x in Witnesses)
                x.SetIds(this);
        }
        public void SetAppealHearing()
        {
            if(appealNumber > 0)
            {
                AppealHearing = Services.ChessService.Hearings.FirstOrDefault(x => x.CaseNumber == appealNumber);
                AppealHearing.AnAppealOf = this;
            }
        }

        public List<CoAWitness> Witnesses = new List<CoAWitness>();

    }
}
