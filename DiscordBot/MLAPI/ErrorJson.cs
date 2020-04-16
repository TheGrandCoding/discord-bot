using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class ErrorJson
    {
        public List<ErrorItem> errors { get; set; }

        public ErrorJson(List<ErrorItem> e)
        {
            errors = e;
        }

        public ErrorJson(string reason) : this(new List<ErrorItem>() { new ErrorItem(null, reason) })
        {
        }
    }
    public class ErrorItem
    {
        public string path { get; set; }
        public string reason { get; set; }
        public ErrorItem(string p, string r)
        {
            path = p; reason = r;
        }
    }
}
