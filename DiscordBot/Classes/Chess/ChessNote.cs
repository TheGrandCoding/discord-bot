using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Chess
{
    public class ChessNote
    {
        [JsonIgnore]
        public BotUser Author { get; set; }

        public DateTime Date { get; set; }

        [JsonProperty("days")]
        public int DaysExpire { get; set; }

        [JsonIgnore]
        public ulong authorIdTemp;

        [JsonIgnore]
        public bool Expired => Date.AddDays(DaysExpire) <= DateTime.Now;

        [JsonProperty("auth")]
        private ulong authoRid => Author?.Id ?? authorIdTemp;

        [JsonConstructor]
        private ChessNote(ulong authorId)
        {
            authorIdTemp = authorId;
            Author = Program.GetUserOrDefault(authorId);
        }

        public string Note { get; set; }

        public ChessNote(BotUser user, string note, int expirataion = 31)
        {
            Date = DateTime.Now;
            Note = note;
            Author = user;
            DaysExpire = expirataion;
        }
    }
}
