using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace DiscordBot.Classes.Chess.Online
{
    public class MoveRefuse : Exception
    {
        public string Title;
        public MoveRefuse(string title, string message) : base(message)
        {
            Title = title;
        }

        public JObject ToJson()
        {
            var jobj = new JObject();
            jobj["t"] = Title;
            jobj["m"] = Message;
            return jobj;
        }
    }
}
