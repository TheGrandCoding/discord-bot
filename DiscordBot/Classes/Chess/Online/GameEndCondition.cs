#if INCLUDE_CHESS
using DiscordBot.WebSockets;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Chess.Online
{
    public class GameEndCondition
    {
        public bool IsOver { get; set; }
        public ChessConnection Winner { get; set; }
        public string Reason { get; set; }
        private GameEndCondition(ChessConnection winner, string reason)
        {
            Winner = winner;
            Reason = reason;
            IsOver = winner != null || reason != null;
        }
        public static GameEndCondition Draw(string reason)
        {
            return new GameEndCondition(null, reason);
        }
        public static GameEndCondition Win(ChessConnection winner, string reason)
        {
            return new GameEndCondition(winner, reason);
        }
        public static GameEndCondition NotEnded()
        {
            return new GameEndCondition(null, null);
        }
    }
}
#endif