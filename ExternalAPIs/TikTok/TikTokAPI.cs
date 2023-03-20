using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExternalAPIs.TikTok
{
    internal class TikTokAPIError
    {
        public string code { get; set; }
        public string message { get; set; }
    }
    internal class TikTokAPIResponse<T>
    {
        public T data { get; set; }
        public TikTokAPIError error { get; set; }
    }
}
