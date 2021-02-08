using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Exceptions
{
    public class MustLoginException : RedirectException
    {
        public MustLoginException() : base("/login", "Must login")
        {
        }
    }
}
