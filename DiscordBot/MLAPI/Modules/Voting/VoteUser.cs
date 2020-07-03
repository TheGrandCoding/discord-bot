using DiscordBot.Classes.Voting;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.MLAPI.Modules.Voting
{
    public class VoteUser : VoteBase
    {
        public VoteUser(APIContext c) : base(c) { }

        string toBase64(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }
        string fromBase64(string base64)
        {
            var bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }


        [Method("GET"), Path("/vote/nouser")]
        public void NoUser()
        {
            string content = toBase64(Context.Id.ToString()) + "." + toBase64(Context.User?.VerifiedEmail ?? "noemail");
            var email = Program.Configuration["vote:adminEmail"];
            email += "?subject=" + Uri.EscapeDataString("Awards Voting Issue");
            email += "&body=" + Uri.EscapeDataString($"Please do not edit this:\r\n" +
                $"{content}\r\n" +
                $"-----------------------------------------------\r\n" +
                $"Please provide any additional information, such as your tutor group:\r\n");
            ReplyFile("nouser.html", 200, new Replacements()
                .Add("email", email)
                .Add("username", EmailName));
        }

        [Method("GET"), Path("/vote")]
        public void VoteBase()
        {
            ReplyFile("vote.html", 200, new Replacements()
                .Add("title", "Y11 Awards"));
        }


        #region API Endpoints
        [Method("POST"), Path("/vote/api/search")]
        public void Search()
        {
            var json = JObject.Parse(Context.Body);
            var nameFilter = json["name"].ToObject<string>();
            var tutorFilter = json["tutor"].ToObject<string>();
            var tb = Service.Database.GetCollection<User>("users");

            var filters = new List<FilterDefinition<User>>();
            if(!string.IsNullOrWhiteSpace(nameFilter))
            {
                filters.Add(
                    Builders<User>.Filter.Or(
                        Builders<User>.Filter.Regex(x => x.FullName, new BsonRegularExpression($"/{nameFilter}/i")),
                        Builders<User>.Filter.Regex(x => x.UserName, new BsonRegularExpression($"/{nameFilter}/i"))
                    )
                );
            }
            if(!string.IsNullOrWhiteSpace(tutorFilter))
            {
                filters.Add(
                    Builders<User>.Filter.Regex(x => x.Tutor, new BsonRegularExpression($"/{tutorFilter}/i"))
                );
            }
            FilterDefinition<User> oneFilter;
            if(filters.Count > 0)
            {
                oneFilter = Builders<User>.Filter.And(filters);
            } else
            {
                oneFilter = FilterDefinition<User>.Empty;
            }
            var finder = tb.Find(oneFilter);

            var list = finder.ToList();
            var res = list.ToJson();
            RespondRaw(res, 200);
        }

        #endregion
    }
}
