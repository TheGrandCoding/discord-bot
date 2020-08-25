using Discord;
using EduLinkDLL;
using EduLinkDLL.Exceptions;
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

        public static void logHandler(EduLinkClient cl, EduLinkDLL.LogMessage m)
        {
            var conv = new Discord.LogMessage((Discord.LogSeverity)m.Severity, $"EL:{(cl.UserName ?? "")}:" + (m.Source ?? ""), m.Message, m.Exception);
            Program.LogMsg(conv);
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
                try
                {
                    client.LoginAsync(x.Value.username, x.Value.password, x.Value.establishment).Wait();
                    Clients[x.Key] = client;
                }
                catch (EduLinkException ex)
                {
                    try
                    {
                        var usr = Program.Client.GetUser(x.Key);
                        usr.SendMessageAsync($"Your EduLink login information has failed; you may use `{Program.Prefix}edulink setup {x.Value.username} [password]` to update it:\r\n" +
                            $">>> {ex.Message}");
                    }
                    catch { }
                }
            }
        }
    }
}
