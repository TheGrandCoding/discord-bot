using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator
{
    public abstract class CalcProcess
    {
        protected static string REG_OR(params string[] expr)
        {
            string s = "(?:";
            foreach (var e in expr)
                s += e + "|";
            s = s.Substring(0, s.Length - 1);
            return s + ")";
        }
        protected static string CONSTANT
        {
            get
            {
                return REG_OR(DiscordBot.Classes.Calculator.Calculator.Constants.Keys.ToArray());
            }
        }
        protected static string DOUBLE_STRICT = @"[\-]?[\d\.]+";
        protected static string LETTER_STRICT = @"[a-z]+";
        protected static string ALPHAN_STRICT = @"[a-zA-Z0-9\.]+";
        protected static string SPC = @"\ ?";
        protected static string DOUBLE = SPC + "(" + REG_OR(DOUBLE_STRICT, CONSTANT) + ")" + SPC;
        protected static string LETTER = SPC + "(" + REG_OR(LETTER_STRICT, CONSTANT) + ")" + SPC;
        protected static string ALPHAN = SPC + "(" + REG_OR(ALPHAN_STRICT, CONSTANT) + ")" + SPC;

        public static bool TryParseDouble(string input, out double result)
        {
            if (Calculator.Constants.TryGetValue(input, out result))
                return true;
            return double.TryParse(input, out result);
        }

        protected bool parseDouble(string input, out double result) => TryParseDouble(input, out result);

        public CalcProcess(Calculator t)
        {
            Calculator = t;
        }
        public Calculator Calculator { get; set; }
        public abstract double Process(string input, Match m);
        protected abstract string RegStr { get; }
        private Regex m_regex;
        public Regex RegEx
        {
            get
            {
                return m_regex ??= new Regex(RegStr);
            }
        }

    }
}
