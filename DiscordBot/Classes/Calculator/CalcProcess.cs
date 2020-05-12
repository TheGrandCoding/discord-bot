using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator
{
    public abstract class CalcProcess
    {
        protected const string STRICT_DOUBLE = @"([\-]?[\d\.]+)";
        protected const string DOUBLE = @"\ ?" + STRICT_DOUBLE + @"\ ?";
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
