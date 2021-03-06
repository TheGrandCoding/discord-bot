﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator
{
    public abstract class TwoSideProcess : CalcProcess
    {
        public TwoSideProcess(Calculator t) : base(t)
        {
        }
        protected abstract string SymbolRegStr { get; }

        protected override string RegStr => DOUBLE + SymbolRegStr + DOUBLE;

        public override double Process(string input, Match m)
        {
            string leftS = m.Groups[1].Value;
            string rightS = m.Groups[2].Value;
            if (!parseDouble(leftS, out var left))
                throw new Exception($"Could not parse '{leftS}' as a number");
            if (!parseDouble(rightS, out var right))
                throw new Exception($"Could not parse '{rightS}' as a number");
            return Process(left, right);
        }

        protected abstract double Process(double left, double right);
    }
}
