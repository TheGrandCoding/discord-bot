using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.MLAPI
{
    public class PathRegex : PathAttribute
    {
        Regex _regex;
        public PathRegex(string regex)
        {
            Path = regex;
            _regex = new Regex(regex);
        }

        public override bool IsMatch(string obj)
        {
            return _regex.IsMatch(obj);
        }
    }
}
