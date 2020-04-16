using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes
{
    public class BotUser
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }
        [JsonProperty("id")]
        public List<AuthToken> Tokens { get; set; } = new List<AuthToken>();

        public List<string> Permissions { get; set; } = new List<string>();
    }
}
