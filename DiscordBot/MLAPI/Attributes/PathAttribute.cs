using Discord;
using DiscordBot.Classes.HTMLHelpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class PathAttribute : Attribute
    {
        protected PathAttribute()
        {
        }

        public string Text { get; protected set; }
        /// <summary>
        /// Where the path is prefixed by a /, and does NOT have a trailing /
        /// </summary>
        public PathAttribute(string path)
        {
            if (!path.StartsWith("/"))
                path = "/" + path;
            if (path.EndsWith("/") && path != "/")
                Program.LogWarning($"Path invalid: '{path}'", "API");
            Text = path;
        }
    }
    [DebuggerDisplay("{Name}={Regex}")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RegexAttribute : Attribute
    {
        public string Name { get; }
        public string Regex { get; }
        public RegexAttribute(string name, string regex)
        {
            Name = name;
            Regex = regex;
            if(regex.StartsWith("(?<"))
            {
                var start = $"(?<{name}>";
                Regex = regex.Substring(start.Length);
                Regex = regex[..^1];
            }
        }
    }
}
