using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class HaltExecutionException : Exception
    {
        // my life
        public virtual bool CompleteFailure { get; } = true;
        public HaltExecutionException(string message) : base(message) { }
        public override string ToString()
        {
            return $"Halted API: {this.Message}";
        }
    }
}
