using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExternalAPIs.TikTok
{
    [Flags]
    public enum TikTokAuthScopes
    {
        UserInfoBasic = 0b001,
        VideoList = 0b010,
        VideoUpload = 0b100,

        All = UserInfoBasic | VideoList | VideoUpload
    }
}
