using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Chess.TimedOnline
{
    public class TimedPacket : Packet<TimedId>
    {
        public TimedPacket(JObject jObj) : base(jObj)
        {
        }
        public TimedPacket(TimedId id, JToken token) : base(id, token)
        {
        }
    }

    public enum TimedId
    {
        Pause,
        Switch,
        Status,
        Start
    }
}
