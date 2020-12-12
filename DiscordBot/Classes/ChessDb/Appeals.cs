using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace DiscordBot.Classes.Chess
{
    public class AppealsHearing
    {
        private AppealsHearing(ChessDbContext db, int id) 
        {
            Id = id;
            Members = db.GetMembersInCase(Id).ToList();
            Motions = db.AppealsMotions.AsQueryable().Where(x => x.HearingId == id).ToList();
            Exhibits = db.AppealsExhibits.AsQueryable().Where(x => x.HearingId == id).ToList();
            Witnesses = db.AppealsWitnesses.AsQueryable().Where(x => x.HearingId == id).ToList();
        }

        public AppealsHearing(IEnumerable<ChessPlayer> claimants, IEnumerable<AppealsMember> respondents)
        {
            Members = new List<AppealsMember>();
            foreach(var x in claimants)
            {
                Members.Add(new AppealsMember(x, this)
                {
                    Relation = Relation.Claimant
                });
            }
            foreach(var x in respondents)
            {
                x.AppealHearing = this;
                Members.Add(x);
            }
        }

        public int Id { get; set; }
        public bool Sealed { get; set; }
        public bool IsArbiterCase { get; set; }

        public List<AppealsMember> Members { get; set; } = new List<AppealsMember>();

        [NotMapped]
        public AppealsMember[] Claimants => Members.Where(x => x.Relation.HasFlag(Relation.Claimant) && !x.Relation.HasFlag(Relation.Counsel)).ToArray();
        [NotMapped]
        public AppealsMember[] Respondents => Members.Where(x => x.Relation.HasFlag(Relation.Respondent) && !x.Relation.HasFlag(Relation.Counsel)).ToArray();
        [NotMapped]
        public AppealsMember[] Justices => Members.Where(x => x.Relation.HasFlag(Relation.Justice) && !x.Relation.HasFlag(Relation.Counsel)).ToArray();

        [InverseProperty("Hearing")]
        public List<AppealsExhibit> Exhibits { get; set; } = new List<AppealsExhibit>();
        public List<AppealsMotion> Motions { get; set; } = new List<AppealsMotion>();
        public List<AppealsWitness> Witnesses { get; set; } = new List<AppealsWitness>();
        public AppealsRuling Ruling { get; set; }

        public string Holding { get; set; }


        string getCapacity(ChessPlayer player)
        {
            if (player == null)
                return "unknown";
            if (player.Permission.HasFlag(ChessPerm.Arbiter))
                return "Arbiter";
            if (player.Permission.HasFlag(ChessPerm.ChiefJustice))
                return "Chief Justice of the Court Of Appeals";
            if (player.Permission.HasFlag(ChessPerm.Justice))
                return "Associate Justice of the Court Of Appeals";
            if (player.Permission.HasFlag(ChessPerm.Moderator))
                return "Moderator";
            return "something";
        }

        public string collate(IEnumerable<AppealsMember> ls)
        {
            var s = "";
            foreach (var member in ls)
            {
                if (member.Relation.HasFlag(Relation.Counsel))
                    continue;
                s += $"{member.Member.Name}";
                if (member.Relation.HasFlag(Relation.OfficialCapacity))
                    s += $", in their official capacity as {getCapacity(member.Member)}";
                s += ";";
            }
            return s[..^1];
        }

        [NotMapped]
        public string Title {  get
            {
                var s = "";
                var clm = Members.Where(x => x.Relation.HasFlag(Relation.Claimant)).ToList();
                var rsp = Members.Where(x => x.Relation.HasFlag(Relation.Respondent)).ToList();
                s += collate(clm);
                s += " v. ";
                s += collate(rsp);
                return s;
            } }

        public int? AppealOf { get; set; }

        public DateTime Filed { get; set; }
        public DateTime? Commenced { get; set; }
        public DateTime? Concluded { get; set; }

        public bool isClerkOnCase(ChessPlayer player)
        {
            return Members.FirstOrDefault(x => x.MemberId == player.Id)?.Relation.HasFlag(Relation.Clerk) ?? false;
        }

        public string getRelationToCase(ChessPlayer player)
        {
            var rel = Members.FirstOrDefault(x => x.MemberId == player.Id)?.Relation ?? Relation.None;
            if (player.Permission == ChessPerm.Arbiter && IsArbiterCase)
                return "Arbiter";
            if (rel == Relation.None)
                return "None";
            if (rel == Relation.Justice)
                return "Justice";
            var s = "";
            if (rel.HasFlag(Relation.Counsel))
                s = "Counsel for ";
            if (rel.HasFlag(Relation.Claimant))
                return s + (AppealOf.HasValue ? "Appellee" : "Claimant");
            if (rel.HasFlag(Relation.Respondent))
                return s + (AppealOf.HasValue ? "Appellant" : "Respondent");
            if (Justices.Length == 0 && player.Permission.HasFlag(ChessPerm.Justice))
                return "Justice";
            return rel.ToString();
        }

        public bool isJudgeOnCase(ChessPlayer player)
        {
            var rel = getRelationToCase(player);
            return rel == "Justice" || rel == "Arbiter";
        }

        public bool CanCallWitness(ChessPlayer player)
        {
            var rel = Members.FirstOrDefault(x => x.MemberId == player.Id)?.Relation ?? Relation.None;
            return rel != Relation.None;
        }
    }

    [Flags]
    public enum Relation
    {
        None             = 0b000000,
        Claimant         = 0b000001,
        Respondent       = 0b000010,
        Justice          = 0b000100,
        OfficialCapacity = 0b001000,
        Counsel          = 0b010000,
        Clerk            = 0b100000,
    }

    [DebuggerDisplay("{Relation,nq} {MemberId,nq}")]
    public class AppealsMember
    {
        public AppealsMember(ChessPlayer player, AppealsHearing hearing)
        {
            Member = player;
            MemberId = player.Id;
            AppealHearing = hearing;
            AppealHearingId = hearing?.Id ?? 0;
        }
        private AppealsMember(ChessDbContext db, int memberId)
        {
            MemberId = memberId;
            Member = db.Players.FirstOrDefault(x => x.Id == memberId);
        }
        public int AppealHearingId { get; set; }
        [ForeignKey("AppealHearingId")]
        public AppealsHearing AppealHearing { get; set; }
        
        public int MemberId { get; set; }
        [ForeignKey("MemberId")]
        public ChessPlayer Member { get; set; }

        public Relation Relation { get; set; }
    }

    public class AppealsWitness
    {
        public int HearingId { get; set; }
        public int WitnessId { get; set; }
        public ChessPlayer Witness { get; set; }
        public DateTime? ConcludedOn { get; set; }
    }

    public class AppealsMotion
    {
        public AppealsMotion()
        {
        }

        private AppealsMotion(ChessDbContext db, int id)
        {
            Id = id;
            Attachments = db.AppealsMotionFiles.AsQueryable().Where(x => x.MotionId == id).ToList();
        }
        public int Id { get; set; }
        public int HearingId { get; set; }
        [ForeignKey("HearingId")]
        public AppealsHearing Hearing { get; set; }
        public string MotionType { get; set; }
        public int MovantId { get; set; }
        public ChessPlayer Movant { get; set; }
        public List<AppealsMotionFile> Attachments { get; set; } = new List<AppealsMotionFile>();

        public string Holding { get; set; }

        public DateTime Filed { get; set; }
        public DateTime? HoldingDate { get; set; }

        [NotMapped]
        public bool Denied
        {
            get
            {
                return Holding != null && (
                    Holding.Contains("deny", StringComparison.OrdinalIgnoreCase)
                    || Holding.Contains("denied", StringComparison.OrdinalIgnoreCase));
            }
        }
        [NotMapped]
        public bool Granted
        {
            get
            {
                return Holding != null && (
                    Holding.Contains("granted", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    public class AppealsRuling
    {
        public int Id { get; set; }
        public int HearingId { get; set; }
        public AppealsHearing Hearing { get; set; }
        public string Holding { get; set; }
        public AppealsAttachment Attachment { get; set; }

        public int SubmitterId { get; set; }
        public ChessPlayer Submitter { get; set; }

        [NotMapped]
        public string DataPath => $"rulings/{Attachment.Id}";
    }

    public class AppealsAttachment
    {
        public AppealsAttachment() { }

        public AppealsAttachment(string fName, int uploaded)
        {
            FileName = fName;
            Filed = DateTime.Now;
            FiledBy = uploaded;
        }

        public int Id { get; set; }
        public DateTime Filed { get; set; }
        public int FiledBy { get; set; }
        public string FileName { get; set; }
    }

    public class AppealsMotionFile
    {
        public int MotionId { get; set; }
        [ForeignKey("MotionId")]
        public AppealsMotion Motion { get; set; }

        public int AttachmentId { get; set; }
        [ForeignKey("AttachmentId")]
        public AppealsAttachment Attachment { get; set; }
    }

    public class AppealsExhibit
    {
        public int HearingId { get; set; }
        [ForeignKey("HearingId")]
        public AppealsHearing Hearing { get; set; }

        public int AttachmentId { get; set; }
        [ForeignKey("AttachmentId")]
        public AppealsAttachment Attachment { get; set; }
    }

    public static class Motions
    {
        public const string WritOfCertiorari = "Motion for writ of certiorari";
        public const string Dismiss = "Motion to dismiss";
        public const string SummaryJudgement = "Motion for summary judgement";
        public const string Join = "Motion to join";
        public const string Seal = "Motion to seal";
    }
}
