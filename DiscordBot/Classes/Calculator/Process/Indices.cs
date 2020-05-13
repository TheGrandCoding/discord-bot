using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator.Process
{
    public class Indices : CalcProcess
    {
        public Indices(Calculator t) : base(t)
        {
        }

        protected override string RegStr => DOUBLE + @"\^" + DOUBLE;

        public override double Process(string input, Match m)
        {
            var digit = m.Groups[1].Value;
            var power = m.Groups[2].Value;
            if (!parseDouble(digit, out var num))
                throw new Exception($"Could not parse number part '{digit}'");
            if (!parseDouble(power, out var pow))
                throw new Exception($"Could not parse power: '{power}'");
            return Math.Pow(num, pow);
        }
    }
}
