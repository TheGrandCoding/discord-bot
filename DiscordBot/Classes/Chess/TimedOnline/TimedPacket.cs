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
            Time = DateTime.Parse(jObj["time"].ToObject<string>());
        }
        public TimedPacket(TimedId id, JToken token) : base(id, token)
        {
            Time = DateTime.Now;
        }
        public DateTime Time { get; set; }

        public override JObject ToJson()
        {
            var b = base.ToJson();
            b["time"] = Time;
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
