using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class APIModule
    {
        public string Name { get; set; }
        public string Summary { get; set; }
        public List<APIPrecondition> Preconditions { get; set; } = new List<APIPrecondition>();
        public List<APIEndpoint> Endpoints { get; set; } = new List<APIEndpoint>();
        public Type Type { get; set; }
    }
}
