using DiscordBot.Classes.Chess;
using DiscordBot.Services;
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
            if(c.User != null)
            {
                SelfPlayer = ChessService.Players.FirstOrDefault(x => x.ConnectedAccount == c.User.Id);
            }
        }
        public ChessBase(APIContext c) : this(c, "chess") { }
        public ChessPlayer SelfPlayer { get; protected set; }

        public string GetPlayerList()
        {
            string players = "";
            foreach (var player in ChessService.Players.Where(x => !x.IsBuiltInAccount).OrderByDescending(x => x.Rating))
            {
                if (player.ShouldContinueInLoop)
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

    }
}
