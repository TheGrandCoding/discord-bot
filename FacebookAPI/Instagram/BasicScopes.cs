using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FacebookAPI.Instagram
{
    [Flags]
    public enum BasicAPIScopes
    {
        UserProfile = 0b01,
        UserMedia   = 0b10,


        All = UserProfile | UserMedia
    }
}
