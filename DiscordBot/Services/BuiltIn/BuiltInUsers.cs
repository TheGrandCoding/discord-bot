using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Services.BuiltIn
{
    public class BuiltInUsers : Service
    {
        public const ulong ChessClass = 3;
        public const ulong ChessCoA = 5;
        public const ulong ChessAI = 15;


        public override void OnLoaded(IServiceProvider services)
        {
        }
    }
}
