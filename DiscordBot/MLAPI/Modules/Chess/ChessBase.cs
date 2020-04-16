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
    }
}
