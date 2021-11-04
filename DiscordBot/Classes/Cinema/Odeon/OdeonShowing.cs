using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DiscordBot.Classes.Cinema.Odeon
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class OdeonShowing : IShowing
    {
        private OdeonShowing(DateTimeOffset start, DateTimeOffset end, bool soldOut, string screen)
        {
            Start = start;
            End = end;
            Screen = screen;
            SoldOut = soldOut;
        }
        internal static OdeonShowing Create(ApiShowtime showtime)
        {
            return new OdeonShowing(showtime.schedule.startsAt, 
                showtime.schedule.endsAt, showtime.isSoldOut, showtime.screenId);
        }

        private string DebuggerDisplay { get
            {
                return $"Screen {Screen} at {Start:yyyy-MM-dd}, {Start:HH:mm}";
            } }

        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }
        public string Screen { get; set; }

        public bool SoldOut { get; set; }


    }
}
