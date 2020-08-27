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
            if (jObj.TryGetValue("t", out var v))
                Time = v.ToObject<long>();
        }
        public TimedPacket(TimedId id, JToken token) : base(id, token)
        {
            Time = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();
        }

        public long? Time { get; set; }

        public override JObject ToJson()
        {
            var b = base.ToJson();
            b["t"] = Time;
            return b;
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
