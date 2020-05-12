using DiscordBot.Websockets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace DiscordBot.Classes.ServerList
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class Server
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("game")]
        public string GameName { get; set; }
        [JsonProperty("id")]
        public Guid Id { get; set; }
        [JsonProperty("players")]
        public List<Player> Players { get; set; }
        [JsonProperty("intIP")]
        public IPAddress InternalIP { get; set; }
        [JsonProperty("extIP")]
        public IPAddress ExternalIP { get; set; }
        [JsonProperty("port")]
        public int Port { get; set; }
        [JsonProperty("private")]
        public bool IsPrivate { get; set; }
        [JsonProperty("online")]
        public bool Online => ActiveSession != null;
        [JsonProperty("auth")]
        public string Authentication { get; set; }

        internal bool PortForwarded { get; set; }
        internal MLServer ActiveSession { get; set; }
    }
}
