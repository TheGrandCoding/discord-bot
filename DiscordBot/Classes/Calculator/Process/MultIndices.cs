using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator.Process
{
    public class MultIndices : CalcProcess
    {
        public MultIndices(Calculator t) : base(t)
        {
        }

        protected override string RegStr => DOUBLE + @"[x\*]10\^";

        public override double Process(string input, Match m)
        {
            throw new ReplaceStringException(input.Replace("x10^", "E"));
        }
    }
}
