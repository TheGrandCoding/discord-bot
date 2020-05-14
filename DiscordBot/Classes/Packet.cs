using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes
{
    public class Packet<T> where T : Enum
    {
        public T Id { get; set; }
        public JToken Content { get; set; }
        public Packet(T id, JToken content)
        {
            Id = id;
            Content = content;
        }

        public override string ToString()
        {
            var jobj = new JObject();
            jobj["id"] = Id.ToString();
            jobj["content"] = Content;
            return jobj.ToString();
        }
    }
}
