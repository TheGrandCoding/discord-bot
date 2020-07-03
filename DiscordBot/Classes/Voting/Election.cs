using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Voting
{
    public class Election
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public string Title { get; set; }

        public UserFlags CanVote { get; set; }
        public UserFlags CanBeVoted { get; set; }

        public Dictionary<string, UserFlags> LocalFlags { get; set; }

        public Dictionary<string, Category> Categories { get; set; }
    }
}
