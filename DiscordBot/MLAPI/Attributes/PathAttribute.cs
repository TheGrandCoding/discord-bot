using Discord;
using DiscordBot.Classes.HTMLHelpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DiscordBot.MLAPI
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
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
            if(path.Contains("(?<"))
            {
                Program.LogWarning($"Path invalid '{path}'. Should use RegexAttribute instead.", "API");
            }

            Text = path;
        }
    }
    [DebuggerDisplay("{Name}={Regex}")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RegexAttribute : Attribute
    {
        public const string SnowflakeRgx = "[0-9]{17,20}";
        public const string Alpha = "[A-Za-z]+";
        public const string Alphanumeric = "[A-Za-z0-9]+";
        public const string AlphaSymbols = "[A-Za-z0-9_]+";
        public const string Filename = AlphaSymbols + @"\." + AlphaSymbols;
        public const string Date = "[0-9]{4}-[0-9]{2}-[0-9]{2}";
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
