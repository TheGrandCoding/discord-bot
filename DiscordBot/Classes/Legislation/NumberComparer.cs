using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Legislation
{
    public class NumberComparer : IComparer<string>
    {
        const string PATTERN = @"([0-9]+)([A-Z]*)(=?)";
        public int Compare([AllowNull] string x, [AllowNull] string y)
        {
            if (x == y || (x == null && y == null))
                return 0;
            if (x == null)
                return -1;
            if (y == null)
                return 1;
            var REGEX = new Regex(PATTERN);
            var a = REGEX.Match(x);
            var b = REGEX.Match(y);

            var aNum = a.Groups[1].Value;
            var bNum = b.Groups[1].Value;
            if(aNum != bNum)
                return int.Parse(aNum).CompareTo(int.Parse(bNum));
            var aLet = a.Groups[2].Value;
            var bLet = b.Groups[2].Value;
            if (aLet != bLet)
                return aLet.CompareTo(bLet);
            var aCheck = a.Groups[3].Value;
            var bCheck = b.Groups[3].Value;
            if (aCheck != null)
                return -1;
            if (bCheck != null)
                return 1;
            return 0;
        }
    }
}
