using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Legislation.Amending
{
    public class AmendmentGroup
    {
        public int Id { get; set; }
        public BotUser Author { get; set; }
        public BotUser[] Contributors { get; set; }
        public DateTime Date { get; set; }
        public bool Draft { get; set; }
    }
}
