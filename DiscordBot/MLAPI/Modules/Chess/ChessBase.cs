using DiscordBot.Classes.Chess;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    public abstract class ChessBase : APIBase
    {
        public ChessBase(APIContext c, string path) : base(c, path)
        {
            DB = Program.Services.GetRequiredService<ChessDbContext>();
            if(c.User != null)
            {
                SelfPlayer = DB.Players.FirstOrDefault(x => x.DiscordAccount == ChessService.cast(c.User.Id));
            }
        }
        public ChessBase(APIContext c) : this(c, "chess") { }
        public ChessPlayer SelfPlayer { get; protected set; }
        public ChessDbContext DB { get; set; }


        public Select GetPlayerList(string id, Func<ChessPlayer, bool> filter = null)
        {
            var sel = new Select(id: id);
            foreach (var player in DB.Players.AsQueryable().Where(x => !x.IsBuiltInAccount).OrderByDescending(x => x.Rating))
            {
                if (player.IsBuiltInAccount || player.Removed)
                    continue;
                if (filter != null && !filter(player))
                    continue;
                var opt = new Option(player.Name, player.Id.ToString());
                if (player.IsBanned)
                {
                    opt.Class = "banned";
                    opt.Disabled = true;
                }
                sel.Children.Add(opt);
            }
            return sel;
        }

        public string GetPlayerList()
        {
            string players = "";
            foreach (var player in DB.Players.AsQueryable().Where(x => !x.IsBuiltInAccount).OrderByDescending(x => x.Rating))
            {
                if (player.IsBuiltInAccount || player.Removed)
                    continue;
                string bannnn = "";
                if (player.IsBanned)
                {
                    bannnn = "disabled class='banned'";
                }
                players += $"<option {bannnn} value=\"{player.Id}\">{player.Name}</option>";
            }
            return players;
        }


        protected bool canClassRoomAccn(ChessPlayer player)
        {
            if (player == null)
                return false;
            if (player.Id == ChessService.BuiltInClassRoomChess)
            {
                if (DateTime.Now.DayOfWeek == DayOfWeek.Friday)
                {
                    var halfTwelve = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                        12, 30, 0);
                    var tenPastOne = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                        13, 20, 0); // allow an extra ten minutes just in case
                    return DateTime.Now >= halfTwelve && DateTime.Now <= tenPastOne;
                }
                return false;
            }
            return doesHavePerm(ChessPerm.AddMatch, player);
        }

        protected bool doesHavePerm(ChessPerm perm, ChessPlayer user)
        {
            if (perm == ChessPerm.Player)
                return true;
            if (user == null)
                return false;
            return user.Permission.HasFlag(perm);
        }

        protected bool doesHavePerm(ChessPerm perm, Classes.BotUser bUser)
        {
            if (bUser == null)
                return perm == ChessPerm.Player;
            if (SelfPlayer != null)
            {
                if (ChessPerm.ClassRoom.HasFlag(perm) || perm == ChessPerm.ClassRoom)
#if DEBUG
                    return true;
#else
                    return canClassRoomAccn(SelfPlayer);
#endif
                return doesHavePerm(perm, SelfPlayer);
            }
            return false;
        }

        protected bool doesHavePerm(ChessPerm perm) => doesHavePerm(perm, Context.User);

        public override void ResponseHalted(HaltExecutionException ex)
        {
            if (DB != null)
                DB.Dispose();
            base.ResponseHalted(ex);
        }
        public override void AfterExecute()
        {
            if (DB != null)
                DB.Dispose();
        }
    }
}
