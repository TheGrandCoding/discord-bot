using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Cinema
{
    public interface IShowing
    {
        string Id => $"{Start.DayOfYear}-{Start:HH-mm}-{Screen}";

        DateTimeOffset Start { get; }
        DateTimeOffset End { get; }

        string Screen { get; }

        bool SoldOut { get; }

        bool Expired => DateTime.Now > Start;

        bool Unavailable => SoldOut || Expired;


    }
}
