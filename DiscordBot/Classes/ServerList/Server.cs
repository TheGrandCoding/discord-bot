using DiscordBot.Websockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace DiscordBot.Classes.ServerList
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class Server
    {
        public Server(string name, string gameName, IPAddress internalIP, IPAddress externalIP, int port, bool isPrivate = false, string authentication = null)
        {
            Name = name;
            GameName = gameName;
            m_players = new List<Player>();
            InternalIP = internalIP;
            ExternalIP = externalIP;
            Port = port;
            IsPrivate = isPrivate;
            Authentication = authentication ?? AuthToken.Generate(32);
        }
        private Server(Server clone, bool safeClone = true) 
            : this(clone.Name, clone.GameName, clone.InternalIP, clone.ExternalIP, clone.Port, clone.IsPrivate, clone.Authentication)
        {
            Id = clone.Id;
            Players = clone.Players;
            ActiveSession = clone.ActiveSession; // for Online to display properly
            if (safeClone)
                Authentication = null;
        }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("game")]
        public string GameName { get; set; }
        [JsonProperty("id")]
        public Guid Id { get; set; }
        List<Player> m_players;
        [JsonProperty("players")]
        public List<Player> Players
        {
            get
            {
                // An offline server cannot have any servers on it.
                return Online ? m_players : new List<Player>();
            } set
            {
                m_players = value;
            }
        }
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
        [JsonProperty("auth", NullValueHandling = NullValueHandling.Ignore)]
        public string Authentication { get; set; }

        public string ToJson()
        {
            // Clients cannot see some information, so return a copy that doesn't have it.
            return Program.Serialise(new Server(this, safeClone: true));
        }

        internal bool PortForwarded { get; set; }
        internal MLServer ActiveSession { get; set; }
    }
}
