using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Chess
{
    public class ChessPlayerComparer : EqualityComparer<ChessPlayer>
    {
        public override bool Equals(ChessPlayer x, ChessPlayer y)
        {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;
            return x.Id == y.Id;
        }

        public override int GetHashCode(ChessPlayer obj)
        {
            if (obj == null)
                return 0;
            return obj.GetHashCode();
        }
    }
}
