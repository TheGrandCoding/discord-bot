using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Voting
{
    public class Category
    {
        [BsonId]
        public int Number { get; set; }
        public string Prompt { get; set; }
        public Dictionary<string, List<string>> Votes { get; set; }
    }
}
