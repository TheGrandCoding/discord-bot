using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator
{
    public class Boolean : CalcProcess
    {
        public Boolean(Calculator t) : base(t)
        {
        }
        protected override string RegStr => ALPHAN + "=" + ALPHAN;

        public override double Process(string input, Match m)
        {
            string leftS = m.Groups[1].Value;
            string rightS = m.Groups[2].Value;
            if (leftS == rightS)
                throw new ReplaceStringException("TRUE");
            if(parseDouble(leftS, out var dL))
            {
                if(parseDouble(rightS, out var dR))
                    throw new ReplaceStringException(dL == dR ? "TRUE" : "FALSE");
            }
            throw new ReplaceStringException("FALSE");
        }
    }
}
