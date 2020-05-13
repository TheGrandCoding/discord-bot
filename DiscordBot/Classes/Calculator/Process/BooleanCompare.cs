using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator
{
    public abstract class booleanBase : CalcProcess
    {
        protected abstract string SymbolReg { get; }
        public booleanBase(Calculator t) : base(t)
        {
        }
        protected override string RegStr => ALPHAN + SymbolReg + ALPHAN;

        bool truey(string s) => s == "TRUE" || s == "1";
        public override double Process(string input, Match m)
        {
            string leftS = m.Groups[1].Value;
            string rightS = m.Groups[2].Value;
            bool o = Process(truey(leftS), truey(rightS));
            throw new ReplaceStringException(o ? "TRUE" : "FALSE");
        }
        public abstract bool Process(bool left, bool right);
    }
    public class BooleanEQ : booleanBase
    {
        public BooleanEQ(Calculator t) : base(t)
        {
        }

        protected override string SymbolReg => "==";

        public override bool Process(bool left, bool right) => left == right;
    }

    public class BooleanOR : booleanBase
    {
        public BooleanOR(Calculator t) : base(t)
        {
        }
        protected override string SymbolReg => @"(?:\|\||OR)";

        public override bool Process(bool left, bool right) => left || right;
    }

    public class BooleanAND : booleanBase
    {
        public BooleanAND(Calculator t) : base(t)
        {
        }
        protected override string SymbolReg => @"(?:&&|AND)";

        public override bool Process(bool left, bool right) => left && right;
    }
}
