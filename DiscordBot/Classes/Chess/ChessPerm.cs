using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Chess
{
    [Flags]
    public enum ChessPerm
    {
        Player =        0b00000000,
        AddMatch =      0b00000001,
        CreateUser =    0b00000010,
        RemoveUser =    0b00000100,
        ClassRoom  =    0b00000011,
        Moderator=      0b00001000 | CreateUser,
        Arbiter =       0b00010000 | Moderator,
        Justice=        0b00100000,
        ChiefJustice=   0b0100000 | Justice | AddMatch | CreateUser | RemoveUser,
    }
}
