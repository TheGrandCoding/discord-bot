using DiscordBot.Classes.Converters;
using DiscordBot.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Chess.COA
{
    public class CoARuling
    {
        public CoARuling()
        {
        }
        public string Short { get; set; }
        public CoAttachment Attachment { get; set; }

        [JsonConverter(typeof(ChessPlayerConverter))]
        public ChessPlayer Submitter { get; set; }

        public void SetIds(CoAHearing h)
        {
            var index = h.Rulings.IndexOf(this);
            Attachment?.SetIds(System.IO.Path.Combine(h.DataPath, "rulings"), index);
        }
    }
}
