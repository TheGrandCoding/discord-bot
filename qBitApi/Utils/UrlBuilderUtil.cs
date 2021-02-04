using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace qBitApi.Utils
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal class UrlBuilder
    {
        Dictionary<string, string> queryString = new Dictionary<string, string>();
        string baseUrl;
        public UrlBuilder(string url)
        {
            baseUrl = url;
        }
        public string this[string key]
        {
            get
            {
                return queryString[key];
            }
            set
            {
                queryString[key] = value;
            }
        }
        public UrlBuilder Add(string key, string value)
        {
            this[key] = value;
            return this;
        }
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(baseUrl);
            bool first = false;
            foreach(var x in queryString)
            {
                sb.Append(first ? "?" : "&");
                sb.Append($"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}");
                first = true;
            }
            return sb.ToString();
        }
        private string DebuggerDisplay => ToString();
    }
}
