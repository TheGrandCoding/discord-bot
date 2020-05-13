using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Calculator
{
    public class ReplaceStringException : Exception
    {
        public ReplaceStringException(string result) : base(result) { }
    }
}
