﻿using Newtonsoft.Json;
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
        protected uint getNext()
        {
            return getNext(counter);
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
            Response = jObj["res"]?.Value<uint?>();
            Id = (T)Enum.Parse(typeof(T), jObj["id"].Value<string>());
            Content = jObj["content"];
        }

        public virtual JObject ToJson()
        {
            var jobj = new JObject();
            jobj["seq"] = Sequence;
            if (Response.HasValue)
                jobj["res"] = Response.Value;
            jobj["id"] = Id.ToString();
            jobj["content"] = Content;
            return jobj;
        }

        public string ToString(Formatting formatting)
            => ToJson().ToString(formatting);
        public override string ToString()
            => this.ToString(Formatting.None);
    }
}
