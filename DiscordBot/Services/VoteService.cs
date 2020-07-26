using DiscordBot.Classes.Voting;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DiscordBot.Services
{
    public class VoteService : Service
    {
        public override bool IsEnabled => false; 
        public Semaphore Lock { get; set; }
        public IMongoDatabase Database { get; set; }

        private IMongoCollection<Election> m_elections;
        public IMongoCollection<Election> Elections { get
            {
                return m_elections ??= Database.GetCollection<Election>("elections");
            } }

        private IMongoCollection<User> m_users;
        public IMongoCollection<User> Users {  get
            {
                return m_users ??= Database.GetCollection<User>("users");
            } }


        public override void OnReady()
        {
            Lock = new Semaphore(1, 1);
            string password = Program.Configuration["tokens:atlas:password"];
            string dbName = "awards";
            var client = new MongoDB.Driver.MongoClient(
                $"mongodb+srv://rpi4:{password}@cluster0-mz2ro.mongodb.net/{dbName}?retryWrites=true&w=majority");
            Database = client.GetDatabase("awards");
        }
    }
}
