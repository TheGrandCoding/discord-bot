using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DiscordBot.Classes
{
    public class Packet<T> where T : Enum
    {
        static uint counter = 1;
        public uint Sequence { get; set; }
        public uint? Response { get; set; }
        public T Id { get; set; }
        public JToken Content { get; set; }

        public Packet(T id, JToken token) 
        {
            Id = id;
            Content = token;
        }

        public Packet<T> ReplyWith(JToken content)
        {
            var pong = new Packet<T>(Id, content);
            pong.Sequence = getNext(counter);
            pong.Response = Sequence;
            return pong;
        }

        uint getNext(uint input)
        {
            if (input == uint.MaxValue)
                counter = 1;
            else
                counter = counter + 1;
            return counter;
        } 

        public Packet(JObject jObj)
        {
            if(jObj.TryGetValue("seq", out var t))
            {
                Sequence = t.Value<uint?>() ?? counter;
            } else
            {
                Sequence = 0;
            }
            Response = jObj["res"].Value<uint?>();
            Id = jObj["id"].Value<T>();
            Content = jObj["content"];
        }

        public override string ToString()
        {
            var jobj = new JObject();
            jobj["seq"] = Sequence;
            if (Response != null)
                jobj["res"] = Response;
            jobj["id"] = Id.ToString();
            jobj["content"] = Content;
            return jobj.ToString();
        }
    }
}
