using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator.Process
{
    public class SumDice : CalcProcess
    {
        public SumDice(Calculator t) : base(t)
        {
        }

        protected override string RegStr => @"([\d]*)[d]([\d]+)";

        public override double Process(string input, Match m)
        {
            var numRolls = m.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(numRolls))
                numRolls = "1";
            var numSides = m.Groups[2].Value;
            if (!int.TryParse(numRolls, out var rolls))
                throw new Exception($"Could not parse '{numRolls}' as integer");
            if (!int.TryParse(numSides, out var sides))
                throw new Exception($"Could not parse '{numSides}' as integer");
            var results = new List<int>();
            for(int i = 0; i < rolls; i++)
            {
                results.Add(Program.RND.Next(1, sides + 1));
            }
            Calculator.AddStep($"| Rolls: " + string.Join(", ", results));
            return results.Sum();
        }
    }
}
