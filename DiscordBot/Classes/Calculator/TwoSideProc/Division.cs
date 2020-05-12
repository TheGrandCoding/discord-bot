using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator.Process
{
    public class Division : TwoSideProcess
    {
        public Division(Calculator t) : base(t)
        {
        }

        protected override string SymbolRegStr => @"[\/]";

        protected override double Process(double left, double right)
            => left / right;
    }
}
