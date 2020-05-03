using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator.Process
{
    public class Addition : CalcProcess
    {
        public Addition(Calculator t) : base(t)
        {
        }

        protected override string RegStr => @"([\d\.\-]+)\ ?[\+]\ ?([\d\.\-]+)";

        public override string Process(string input, Match m)
        {
            string leftS = m.Groups[1].Value;
            string rightS = m.Groups[2].Value;
            if (!double.TryParse(leftS, out var left))
                throw new Exception($"Could not parse '{leftS}' as a number");
            if (!double.TryParse(rightS, out var right))
                throw new Exception($"Could not parse '{rightS}' as a number");
            return (left + right).ToString();
        }
    }
}
