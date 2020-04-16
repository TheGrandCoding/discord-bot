using Discord;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class PathAttribute : Attribute
    {
        protected PathAttribute()
        {
        }
        public string Path { get; protected set; }
        /// <summary>
        /// Where the path is prefixed by a /, and does NOT have a trailing /
        /// </summary>
        public PathAttribute(string path)
        {
            if (!path.StartsWith("/"))
                path = "/" + path;
            if (path.EndsWith("/") && path != "/")
                Program.LogMsg($"Path invalid: '{path}'", source:"API", sev:LogSeverity.Warning);
            Path = path;
        }

        public virtual bool IsMatch(string query)
        {
            return query == Path;
        }
    }
}
