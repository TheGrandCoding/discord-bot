using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Classes.Cinema
{
    public interface ICinema
    {
        string Name { get; }
        string Location { get; }

        bool CanAutocomplete { get; }
        int DaysFetched { get; }

        Task<IReadOnlyCollection<IFilm>> GetFilmsAsync(DateTimeOffset startDate, DateTimeOffset endDate);
        Task<IReadOnlyCollection<IFilm>> GetFilmsAsync();
        Task<IFilm> GetFilmAsync(string id);
    }
}
