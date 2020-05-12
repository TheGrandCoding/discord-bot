using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator.Process
{
    public class Brackets : CalcProcess
    {
        public Brackets(Calculator t) : base(t)
        {
        }
        protected override string RegStr => @"(?<![a-z])\((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!))\)";
        public override double Process(string input, Match m)
        {
            var subC = new Calculator(Calculator);
            return subC.Output(input.Substring(1, input.Length - 2));
        }
    }
}
