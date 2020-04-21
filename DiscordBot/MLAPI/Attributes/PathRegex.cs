using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.MLAPI
{
    public class PathRegex : PathAttribute
    {
        public Regex Regex { get; private set; }
        public Match RgxMatch { get; private set; }
        public PathRegex(string regex)
        {
            Path = regex;
            Regex = new Regex(regex);
        }

        public override bool IsMatch(string obj)
        {
            RgxMatch = Regex.Match(obj);
            return RgxMatch.Success;
        }
    }
}
