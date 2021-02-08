using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI
{
    /// <summary>
    /// Causes execution to halt and a redirect to occur
    /// </summary>
    public class RedirectException : HaltExecutionException
    {
        public override bool CompleteFailure => false;
        public string URL { get; set; }
        public RedirectException(string url, string m) : base(m)
        {
            URL = url;
        }
        public override string ToString()
        {
            return $"Must redirect: {URL}";
        }
    }
}
