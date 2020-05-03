using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator
{
    public abstract class CalcProcess
    {
        public CalcProcess(Calculator t)
        {
            Calculator = t;
        }
        public Calculator Calculator { get; set; }
        public abstract string Process(string input, Match m);
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
