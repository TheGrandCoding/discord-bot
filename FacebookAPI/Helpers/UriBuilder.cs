using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FacebookAPI.Helpers
{
    public static class UriHelpers
    {
        public static UriBuilder WithQuery(this UriBuilder builder, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(builder.Query))
                builder.Query = "?" + Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value);
            else
                builder.Query += "&" + Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value);
            return builder;
        }
    }
}
