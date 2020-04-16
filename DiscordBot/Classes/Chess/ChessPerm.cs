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
        Elected =       0b10000000,
        ClassRoom  =    AddMatch | CreateUser,
        Moderator=      0b00001000 | CreateUser,
        Justice=        0b00010000 | Moderator,
        CourtOfAppeals= 0b00111111,
        ElectedMod =    Elected | Moderator
    }
}
