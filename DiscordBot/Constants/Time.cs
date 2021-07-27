using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot
{
    /// <summary>
    /// Constants to perform math with time
    /// </summary>
    public static class Time
    {
        /// <summary>
        /// Time as miliseconds
        /// </summary>
        public static class Ms
        {
            public const int Milisecond = 1;
            public const int Second = 1000;
            public const int Minute = Second * 60;
            public const int Hour = Minute * 60;
            public const int Day = Hour * 24;
        }

        /// <summary>
        /// Time in seconds
        /// </summary>
        public static class S
        {
            public const int Second = 1;
            public const int Minute = Second * 60;
            public const int Hour = Minute * 60;
            public const int Day = Hour * 24;
        }
    }
}
