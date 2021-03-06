﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Chess.Online
{
    public abstract class APIObject
    {
        public virtual void LoadJson(ChessClient.Classes.Chess.ChessPacket p) => LoadJson(p.Content);
        public abstract void LoadJson(JToken json);
        public abstract JObject ToJson();
    }
    public abstract class APIObject<T> : APIObject
    {
        public T Id { get; set; }
    }
}
