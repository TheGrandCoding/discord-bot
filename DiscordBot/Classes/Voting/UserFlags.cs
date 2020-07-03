using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Voting
{
    [Flags]
    public enum UserFlags
    {
        None = 0,

        Student = 0b0000_0001,
        Staff   = 0b0000_0010,
        Helper  = 0b0000_0100,
        Sysop   = 0b0000_1000,
    }
}
