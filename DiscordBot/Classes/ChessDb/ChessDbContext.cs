using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Classes.Chess
{
    public class ChessContextFactory : IDesignTimeDbContextFactory<ChessDbContext>
    {
        public ChessDbContext CreateDbContext(string[] args)
        {
            Program.LogMsg(Program.GetStackTrace(), LogSeverity.Info, $"Chs-Factory");
            var builder = new DbContextOptionsBuilder<ChessDbContext>();
            builder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=chsData;MultipleActiveResultSets=true");
            builder.EnableSensitiveDataLogging();
            return new ChessDbContext(builder.Options);
        }
    }
    public class ChessDbContext : DbContext
    {
        public ChessDbContext([NotNull] DbContextOptions<ChessDbContext> options) : base(options)
        {
        }

        public ChessDbContext()
        {
        }

        public DbSet<ChessPlayer> Players { get; set; }
        public DbSet<ChessGame> Games { get; set; }
        public DbSet<ChessBan> Bans { get; set; }
        public DbSet<ChessNote> Notes { get; set; }
        public DbSet<ChessInvite> Invites { get; set; }

        public DbSet<AppealsHearing> Appeals { get; set; }
        public DbSet<AppealsMember> AppealsRelations { get; set; }
        public DbSet<AppealsAttachment> AppealsAttachments { get; set; }
        public DbSet<AppealsMotion> AppealsMotions { get; set; }
        public DbSet<AppealsMotionFile> AppealsMotionFiles { get; set; }
        public DbSet<AppealsExhibit> AppealsExhibits { get; set; }
        public DbSet<AppealsWitness> AppealsWitnesses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChessGame>()
                .HasIndex(x => new { x.Id, x.WinnerId, x.LoserId });
            modelBuilder.Entity<AppealsHearing>()
                .HasMany(x => x.Exhibits)
                .WithOne(x => x.Hearing);
            modelBuilder.Entity<AppealsHearing>()
                .HasMany(x => x.Motions)
                .WithOne(x => x.Hearing);
            modelBuilder.Entity<AppealsHearing>()
                .HasMany(x => x.Members)
                .WithOne(x => x.AppealHearing);
            modelBuilder.Entity<AppealsMotion>()
                .HasMany(x => x.Attachments)
                .WithOne(x => x.Motion);
            modelBuilder.Entity<AppealsMember>()
                .HasKey(x => new { x.MemberId, x.AppealHearingId });
            modelBuilder.Entity<ArbiterVote>()
                .HasKey(x => new { x.VoterId, x.VoteeId });
            modelBuilder.Entity<ChessPlayer>()
                .HasMany(x => x.ArbVotes)
                .WithOne(x => x.Voter);
            modelBuilder.Entity<ChessPlayer>()
                .HasMany(x => x.Bans)
                .WithOne(x => x.Target);
            modelBuilder.Entity<ChessPlayer>()
                .HasMany(x => x.GamesWon)
                .WithOne(x => x.Winner);
            modelBuilder.Entity<ChessPlayer>()
                .HasMany(x => x.GamesLost)
                .WithOne(x => x.Loser);
            modelBuilder.Entity<ChessPlayer>()
                .HasMany(x => x.Notes)
                .WithOne(x => x.Target);
            modelBuilder.Entity<ChessPlayer>()
                .HasMany(x => x.DateScores)
                .WithOne(x => x.Player);
            modelBuilder.Entity<ChessDateScore>()
                .HasKey(x => new { x.Date, x.PlayerId });
            modelBuilder.Entity<ChessPlayer>()
                .HasMany(x => x.Appeals)
                .WithOne(x => x.Member);
            modelBuilder.Entity<ChessPlayer>()
                .HasMany(x => x.Motions)
                .WithOne(x => x.Movant);
            modelBuilder.Entity<AppealsWitness>()
                .HasKey(x => new { x.HearingId, x.WitnessId });

            modelBuilder.Entity<ChessGame>()
                .HasOne(x => x.Winner)
                .WithMany(x => x.GamesWon)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ChessGame>()
                .HasOne(x => x.Loser)
                .WithMany(x => x.GamesLost)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<AppealsMotion>()
                .HasMany(x => x.Attachments)
                .WithOne(x => x.Motion);
            modelBuilder.Entity<AppealsMotionFile>()
                .HasKey(x => new { x.MotionId, x.AttachmentId });
            modelBuilder.Entity<AppealsExhibit>()
                .HasKey(x => new { x.HearingId, x.AttachmentId });
        }

        public IQueryable<ChessGame> GetGamesWith(ChessPlayer player)
        {
            return GetGamesWith(player.Id);
        }
        public IQueryable<ChessGame> GetGamesWith(int id)
        {
            return Games.AsQueryable().Where(x => x.WinnerId == id || x.LoserId == id);
        }
        public IEnumerable<ChessGame> GetCurrentGamesWith(ChessPlayer player)
            => GetCurrentGamesWith(player.Id);
        public IEnumerable<ChessGame> GetCurrentGamesWith(int id)
            => GetGamesWith(id).ToList().Where(x => x.IsApproved);
        public IQueryable<ChessGame> GetGamesOnDate(int id, DateTime date)
            => GetGamesWith(id).Where(x => x.Timestamp.Year == date.Year && x.Timestamp.DayOfYear == date.DayOfYear);
        public IQueryable<ChessBan> GetBansAgainst(ChessPlayer player)
            => GetBansAgainst(player.Id);
        public IQueryable<ChessBan> GetBansAgainst(int id)
            => Bans.AsQueryable().Where(x => x.TargetId == id);
        public IQueryable<ChessNote> GetNotesAgainst(int id)
            => Notes.AsQueryable().Where(x => x.TargetId == id);

        #region Appeals

        public IQueryable<AppealsMember> GetMembersInCase(int id)
            => AppealsRelations.AsQueryable().Where(x => x.AppealHearingId == id);

        #endregion
    }

    [DebuggerDisplay("{Id} {Name} {Permission}")]
    public class ChessPlayer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Chess.ChessPerm Permission { get; set; }

        public bool Removed { get; set; }
        public bool RequireTiming { get; set; }
        public bool RequireGameApproval { get; set; }
        public bool WithdrawnModVote { get; set; }

        public long DiscordAccount { get; set; }

        [NotMapped, Obsolete]
        public ulong ConnectedAccount
        {
            get
            {
                unchecked
                {
                    return (ulong)DiscordAccount;
                }
            }
            set
            {
                unchecked
                {
                    DiscordAccount = (long)value;
                }
            }
        }
        
        public DateTime? DateLastPresent { get; set; }

        public bool IsBuiltInAccount { get; set; }
    
        public string DismissalReason { get; set; }

        public int Rating { get; set; } = Services.ChessService.StartingValue;
        public int Modifier { get; set; }

        public int Wins { get; set; }
        public int Losses { get; set; }


        public List<ArbiterVote> ArbVotes { get; set; } = new List<ArbiterVote>();
        public List<ChessBan> Bans { get; set; } = new List<ChessBan>();
        [InverseProperty("Winner")]
        public List<ChessGame> GamesWon { get; set; } = new List<ChessGame>();
        [InverseProperty("Loser")]
        public List<ChessGame> GamesLost { get; set; } = new List<ChessGame>();
        public List<ChessNote> Notes { get; set; } = new List<ChessNote>();
        public List<ChessDateScore> DateScores { get; set; } = new List<ChessDateScore>();
        public List<AppealsMember> Appeals { get; set; } = new List<AppealsMember>();
        public List<AppealsMotion> Motions { get; set; } = new List<AppealsMotion>();

        [NotMapped]
        public bool IsBanned => Bans.Any(x => x.IsActive);

        [NotMapped]
        public List<ChessNote> ActiveNotes => Notes.Where(x => x.IsActive).ToList();
        [NotMapped]
        public bool ShouldContinueInLoop => this.Removed || this.IsBuiltInAccount;
        [NotMapped]
        public string VerifyOnlineReference { get; set; }
        [NotMapped]
        public double WinRate
        {
            get
            {
                double total = Wins + Losses;
                if (total == 0)
                    return 0;
                return (double)Wins / total;
            }
        }

        public int GetScoreOnDay(DateTime date)
        {
            int value = Services.ChessService.StartingValue;
            foreach(var x in DateScores)
            {
                if (x.Date < date)
                    value = x.Score;
                else if (x.Date > date)
                    break;
            }
            return value;
        }

        public void SetScoreOnDay(int score, DateTime date)
        {
            DateScores.Add(new ChessDateScore(this)
            {
                Date = date,
                Score = score
            });
        }

    }

    public class ChessDateScore
    {
        public ChessDateScore (ChessPlayer player)
        {
            Player = player;
            PlayerId = player.Id;
        }
        private ChessDateScore(ChessDbContext db, int playerId)
        {
            PlayerId = playerId;
            Player = db.Players.FirstOrDefault(x => x.Id == playerId);
        }
        public DateTime Date { get; set; }
     
        [ForeignKey("PlayerId")]
        public ChessPlayer Player { get; set; }
        public int PlayerId { get; set; }

        public int Score { get; set; }
    }

    public class ChessInvite
    {
        public long Id { get; set; }
        public string Code { get; set; }
    }

    public class ChessGame
    {
        private ChessGame(ChessDbContext db, int winnerId, int loserId)
        {
            WinnerId = winnerId;
            LoserId = loserId;
            Winner = db.Players.FirstOrDefault(x => x.Id == winnerId);
            Loser = db.Players.FirstOrDefault(x => x.Id == loserId);
        }
        public ChessGame(ChessPlayer winner, ChessPlayer loser) 
        {
            Loser = loser;
            LoserId = loser?.Id ?? 0;
            Winner = winner;
            WinnerId = winner?.Id ?? 0;
        }
        public int Id { get; set; }
        public int WinnerId { get; set; }
        [ForeignKey("WinnerId")]
        public ChessPlayer Winner { get; set; }
        public int LoserId { get; set; }
        [ForeignKey("LoserId")]
        public ChessPlayer Loser { get; set; }

        public bool Draw { get; set; }

        public int WinnerChange { get; set; }
        public int LoserChange { get; set; }

        public DateTime Timestamp { get; set; }

        public ApprovedBy ApprovalNeeded { get; set; }
        public ApprovedBy ApprovalGiven { get; set; }

        [NotMapped]
        public bool NeedsModApproval { get
            {
                if (ApprovalNeeded.HasFlag(ApprovedBy.Moderator))
                    return !ApprovalGiven.HasFlag(ApprovedBy.Moderator);
                return false;
            }
        }

        [NotMapped]
        public bool NeedsWinnerApproval
        {
            get
            {
                if (ApprovalNeeded.HasFlag(ApprovedBy.Winner))
                    return !ApprovalGiven.HasFlag(ApprovedBy.Winner);
                return false;
            }
        }

        [NotMapped]
        public bool NeedsLoserApproval
        {
            get
            {
                if (ApprovalNeeded.HasFlag(ApprovedBy.Loser))
                    return !ApprovalGiven.HasFlag(ApprovedBy.Loser);
                return false;
            }
        }

        [NotMapped]
        public bool IsApproved {  get
            {
                return ((int)ApprovalGiven & (int)ApprovalNeeded) == (int)ApprovalNeeded;
            } }
    }

    public class ArbiterVote
    {
        public ArbiterVote(ChessPlayer voter) 
        {
            Voter = voter;
            VoterId = voter.Id;
        }
        private ArbiterVote(ChessDbContext db, int voterId)
        {
            VoterId = voterId;
            Voter = db.Players.FirstOrDefault(x => x.Id == voterId);
        }
        public int VoterId { get; set; }
        [ForeignKey("VoterId")]
        public ChessPlayer Voter { get; set; }
        public int VoteeId { get; set; }
        public int Score { get; set; }
    }

    [Flags]
    public enum ApprovedBy
    {
        None        = 0b000,
        Moderator   = 0b001,
        Winner      = 0b010,
        Loser       = 0b100,
    }

    public class ChessBan
    {
        public ChessBan() { }
        public ChessBan(ChessPlayer target, ChessPlayer oper)
        {
            TargetId = target.Id;
            Target = target;
            OperatorId = oper.Id;
        }
        public int Id { get; set; }
        public int OperatorId { get; set; }
        [ForeignKey("OperatorId")]
        public ChessPlayer Operator { get; set; }
        public int TargetId { get; set; }
        [ForeignKey("TargetId")]
        public ChessPlayer Target { get; set; }

        public DateTime GivenAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Reason { get; set; }

        [NotMapped]
        public bool IsActive => DateTime.Now > GivenAt && ExpiresAt > DateTime.Now;
    }

    public class ChessNote
    {
        public ChessNote() { }
        public ChessNote(ChessPlayer oper, ChessPlayer target, string text, int expires)
        {
            OperatorId = oper?.Id ?? 0;
            Target = target;
            TargetId = target.Id;
            GivenAt = DateTime.Now;
            ExpiresInDays = expires;
            Text = text;
        }
        public int Id { get; set;}
        public int OperatorId { get; set; }
        [ForeignKey("OperatorId")]
        public ChessPlayer Operator { get; set; }
        public int TargetId { get; set; }
        [ForeignKey("TargetId")]
        public ChessPlayer Target { get; set; }
        public DateTime GivenAt { get; set; }
        public int ExpiresInDays { get; set; }
        public string Text { get; set; }

        [NotMapped]
        public DateTime ExpiresAt => GivenAt.AddDays(ExpiresInDays);

        [NotMapped]
        public bool IsActive => DateTime.Now > GivenAt && ExpiresAt > DateTime.Now;
    }
}
