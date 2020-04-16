using System;
using System.Collections.Generic;
using System.Text;
using DiscordBot.WebSockets;
using Newtonsoft.Json.Linq;

namespace DiscordBot.Classes.Chess.Online
{
    public class ConnectionBroadcast : APIObject
    {
        public ChessConnection Player;
        public string Mode;
        public ConnectionBroadcast(ChessConnection p, string m)
        {
            Player = p;
            Mode = m;
        }
        public override void LoadJson(JObject json)
        {
        }

        public override JObject ToJson()
        {
            var jobj = new JObject();
            jobj["player"] = Player.ToJson();
            jobj["mode"] = Mode;
            return jobj;
        }
    }
}
