using EduLinkDLL;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Services
{
    public class EduLinkService : EncryptedService
    {
        protected override string KeyLocation => "edulink_service";

        public override string GenerateSave()
        {
            var dict = new Dictionary<ulong, userInfo>();
            foreach(var keypair in Clients)
            {
                dict.Add(keypair.Key, new userInfo()
                {
                    username = keypair.Value.CurrentUser.Username,
                    password = keypair.Value.CurrentUser.Password,
                    establishment = keypair.Value.Establishment.Id
                });
            }
            return Program.Serialise(dict);
        }

        class userInfo
        {
            public string username { get; set; }
            public string password { get; set; }
            public int establishment { get; set; }
        }
        public Dictionary<ulong, EduLinkClient> Clients { get; set; }

        void logHandler(EduLinkClient sender, string message)
        {
            Program.LogMsg(message, Discord.LogSeverity.Info, $"EL:{sender.UserName}");
        }


        public override void OnReady()
        {
            var content = ReadSave();
            var dict = Program.Deserialise<Dictionary<ulong, userInfo>>(content);
            Clients = new Dictionary<ulong, EduLinkClient>();
            foreach(var x in dict)
            {
                var client = new EduLinkClient();
                client.Log = logHandler;
                var ok = client.LoginAsync(x.Value.username, x.Value.password, x.Value.establishment).Result;
                if(ok)
                {
                    Clients[x.Key] = client;
                }
            }
        }
    }
}
