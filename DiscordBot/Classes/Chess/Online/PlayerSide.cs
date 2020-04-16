using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Chess.Online
{
    [Flags]
    public enum PlayerSide
    {
        None = 0b00,
        White =0b01,
        Black =0b10,
    }
}
