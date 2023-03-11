using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExternalAPIs.Facebook
{
    [Flags]
    public enum OAuthScopes
    {
        PublicProfile               = 0b00000001,
        Email                       = 0b00000010,
        InstagramBasic              = 0b00000100,
        InstagramContentPublish     = 0b00001000,
        InstagramManageComments     = 0b00010000,
        InstagramManageInsights     = 0b00100000,
        InstagramManageMessages     = 0b01000000,
        PagesShowList               = 0b10000000
    }
}
