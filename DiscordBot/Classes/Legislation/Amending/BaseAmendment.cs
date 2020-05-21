using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Legislation.Amending
{
    public abstract class BaseAmendment
    {
        public AmendType Type { get; set; }
        public DateTime Date { get; set; }
        [JsonConverter(typeof(BotUserConverter))]
        public BotUser User { get; set; }
        public abstract string GetDescription();
    }

    public enum AmendType
    {
        Replace,
        Repeal,
        Insert
    }
}
