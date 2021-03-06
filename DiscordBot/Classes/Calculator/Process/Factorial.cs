﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator.Process
{
    public class Factorial : CalcProcess
    {
        public Factorial(Calculator t) : base(t)
        {
        }

        protected override string RegStr => DIGIT + "!";

        public override double Process(string input, Match m)
        {
            var digit = m.Groups[1].Value;
            if (digit.Contains('.'))
                throw new Exception($"Factorial cannot be used on non-whole numbers");
            if (!long.TryParse(digit, out var num))
                throw new Exception($"Could not parse '{digit}' as integer");
            long result = 1;
            for (long i = num; i > 0; i--)
                result *= i;
            return result;
        }
    }
}
