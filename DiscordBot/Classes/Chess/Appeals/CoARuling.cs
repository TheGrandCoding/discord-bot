using DiscordBot.Classes.Converters;
using DiscordBot.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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

        [JsonIgnore]
        public string DataPath { get; set; }

        public void SetIds(AppealHearing h)
        {
            DataPath = Path.Combine(h.DataPath, "rulings");
            Attachment?.SetIds(DataPath, 0);
        }
    }
}
