using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.Cinema.Odeon
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class OdeonFilm : IFilm
    {
        private OdeonFilm(string id, string title, int year, int runtime, IEnumerable<OdeonShowing> showings)
        {
            Id = id;
            Title = title;
            Year = year;
            RuntimeMinutes = runtime;
            Showings = showings.ToArray();
        }

        internal static OdeonFilm Create(ApiFilm film, IEnumerable<OdeonShowing> showings)
        {
            return new OdeonFilm(film.id, film.title.text, 
                film.releaseDate.Year, film.runtimeInMinutes, showings);
        }

        public string Id { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
        public int RuntimeMinutes { get; set; }

        public IReadOnlyCollection<OdeonShowing> Showings { get; set; }

        private string DebuggerDisplay { get
            {
                return $"{Id} {Title} {Year} {Showings.Count} showings";
            } }


        // IFilm
        IReadOnlyCollection<IShowing> IFilm.Showings => Showings;
    }
}
