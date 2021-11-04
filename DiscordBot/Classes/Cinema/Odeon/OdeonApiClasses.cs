using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Cinema.Odeon
{
    internal class ApiMasterData
    {
        public string businessDate { get; set; }

        public List<ApiShowtime> showtimes { get; set; }

        public ApiRelatedData relatedData { get; set; }
    }

    internal class ApiShowtime
    {
        public string id { get; set; }
        public ApiSchedule schedule { get; set; }

        public bool isSoldOut { get; set; }
        public int seatLayoutId { get; set; }
        public string filmId { get; set; }
        public string siteId { get; set; }
        public string screenId { get; set; }
        public string[] attributeIds { get; set; }
        public bool isAllocatedSeating { get; set; }
        public bool required3dGlasses { get; set; }
        public string eventId { get; set; }
    }

    internal class ApiSchedule
    {
        public string businessDate { get; set; }
        public DateTimeOffset startsAt { get; set; }
        public DateTime endsAt { get; set; }
    }

    internal class ApiRelatedData
    {
        public ApiSite[] sites { get; set; }
        public ApiFilm[] films { get; set; }
    }

    internal class ApiSite
    {
        public string id { get; set; }
        public ApiTranslatable name { get; set; }
        public ApiLocation location { get; set; }
    }
    internal class ApiLocation
    {
        public double latitude { get; set; }
        public double longitude { get; set; }
    }
    internal class ApiTranslatable
    {
        public string text { get; set; }
        public string[] translations { get; set; }

        public override string ToString() => text;
    }

    internal class ApiFilm
    {
        public string id { get; set; }
        public ApiTranslatable title { get; set; }
        public ApiTranslatable synopsis { get; set; }
        public ApiTranslatable shortSynopsis { get; set; }
        public ApiTranslatable censorRatingNote { get; set; }

        public DateTimeOffset releaseDate { get; set; }
        public int runtimeInMinutes { get; set; }
        public string trailerUrl { get; set; }

        public string hopk { get; set; }
        public string hoCode { get; set; }
        public string distributorName { get; set; }

    }
}
