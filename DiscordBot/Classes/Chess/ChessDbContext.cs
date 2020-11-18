using DiscordBot.Classes.Chess;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Classes.DbChess
{
    public class ChessDbContext : DbContext
    {
        public ChessDbContext([NotNull] DbContextOptions options) : base(options)
        {
        }

        public DbSet<ChessPlayer> Players { get; set; }
        public DbSet<ChessGame> Games { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChessGame>()
                .HasIndex(x => new { x.Id, x.WinnerId, x.LoserId });
        }

        public IQueryable<ChessGame> GetGamesWith(ChessPlayer player)
        {
            return GetGamesWith(player.Id);
        }
        public IQueryable<ChessGame> GetGamesWith(int id)
        {
            return Games.AsQueryable().Where(x => x.WinnerId == id || x.LoserId == id);
        }
        public IQueryable<ChessGame> GetCurrentGamesWith(ChessPlayer player)
            => GetCurrentGamesWith(player.Id);
        public IQueryable<ChessGame> GetCurrentGamesWith(int id)
            => GetGamesWith(id).Where(x => x.ModApproval != null && x.OtherApproval != null);


        #region Hefty Maths
        static int kFunction(int total)
        {
            if (total <= 3)
                return 40;
            if (total > 3 && total <= 6)
                return 30;
            if (total > 6 && total <= 10)
                return 20;
            return 10;
        }
        static double getExpectedRating(ChessPlayer a, ChessPlayer b)
        {
            var denom = 1 + (Math.Pow(10, (b.Rating - a.Rating) / 400d));
            return 1 / denom;
        }
        public double getRating(ChessPlayer a, ChessPlayer b, double actualScore)
        {
            int total = GetGamesWith(a).Count();
            var update = kFunction(total) * (actualScore - getExpectedRating(a, b));
            return a.Rating + update;
        }
        #endregion

        public async Task AddGame(ChessPlayer winner, ChessPlayer loser, bool draw, bool thirdParty = false, DateTime? when = null)
        {
            var game = new ChessGame()
            {
                WinnerId = winner.Id,
                LoserId = loser.Id,
                Draw = draw,
                Timestamp = when.GetValueOrDefault(DateTime.Now),
            };
            if (thirdParty)
                game.OtherApproval = false;
            if (winner.RequireGameApproval || loser.RequireGameApproval)
                game.ModApproval = false;
            int winnerRating = (int)Math.Round(getRating(winner, loser, draw ? 0.5d : 1.0d));
            game.WinnerChange = winnerRating - winner.Rating;
            int loserRating = (int)Math.Round(getRating(loser, winner, draw ? 0.5d : 0.0d));
            game.LoserChange = loserRating - loser.Rating;
            winner.DateLastPresent = null;
            loser.DateLastPresent = null;
            await Games.AddAsync(game);
            await SaveChangesAsync();
        }
    }

    public class ChessPlayer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ChessPerm Permission { get; set; }

        public bool Removed { get; set; }
        public bool RequireTiming { get; set; }
        public bool RequireGameApproval { get; set; }
        public bool WithdrawnModVote { get; set; }

        public long DiscordAccount { get; set; }

        [NotMapped]
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

        public int Rating { get; set; }
        public int Modifier { get; set; }
    }

    public class ChessGame
    {
        public int Id { get; set; }
        public int WinnerId { get; set; }
        public int LoserId { get; set; }

        public bool Draw { get; set; }

        public int WinnerChange { get; set; }
        public int LoserChange { get; set; }

        public DateTime Timestamp { get; set; }
        public bool? ModApproval { get; set; }
        public bool? OtherApproval { get; set; }
    }

}
