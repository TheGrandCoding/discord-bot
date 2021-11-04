using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Cinema
{
    public interface IFilm
    {
        string Id { get; }
        
        string Title { get; }
        int Year { get; }

        int RuntimeMinutes { get; }

        IReadOnlyCollection<IShowing> Showings { get; }
    }
}
