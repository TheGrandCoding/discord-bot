using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator.Process
{
    public class Multiplication : TwoSideProcess
    {
        public Multiplication(Calculator t) : base(t)
        {
        }

        protected override string SymbolRegStr => @"[x\*]";

        protected override double Process(double left, double right)
            => left * right;
    }
}
