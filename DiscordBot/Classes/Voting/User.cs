using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Voting
{
    public class User
    {
        [BsonId]
        public string UserName { get; set; }

        public UserFlags Flags { get; set; }

        public string Tutor { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}
