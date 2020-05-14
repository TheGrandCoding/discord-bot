using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.ServerList
{
    public class Player
    {
        public string Name { get; set; }
        public int Score { get; set; }
        public int Latency { get; set; }

        public static Player FromJson(JsonReader reader)
        {
            var array = (JArray)JArray.ReadFrom(reader);
            return new Player()
            {
                Name = array[0].ToObject<string>(),
                Score = array[1].ToObject<int>(),
                Latency = array[2].ToObject<int>()
            };
        }
        public void ToJson(JsonWriter writer)
        {
            var jArray = new JArray(Name, Score, Latency);
            jArray.WriteTo(writer);
        }
    }
}
