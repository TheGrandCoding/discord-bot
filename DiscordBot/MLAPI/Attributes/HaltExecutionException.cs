using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Attributes
{
    public class HaltExecutionException : Exception
    {
        public HaltExecutionException(string message) : base(message) { }
        public override string ToString()
        {
            return $"Halted API: {this.Message}";
        }
    }
}
