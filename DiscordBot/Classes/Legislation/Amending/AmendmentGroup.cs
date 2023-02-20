using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Legislation.Amending
{
    public class AmendmentGroup
    {
        public int Id { get; set; }
        public BotDbUser Author { get; set; }
        public BotDbUser[] Contributors { get; set; }
        public DateTime Date { get; set; }
        public bool Draft { get; set; }
    }
}
