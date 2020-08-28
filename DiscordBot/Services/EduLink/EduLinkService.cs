using Discord;
using EduLinkDLL;
using EduLinkDLL.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
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
            if(m.Severity == EduLinkDLL.LogSeverity.Debug && (m.Source == "Response" || m.Source == "Request"))
            {
                var path = Path.Combine(Program.BASE_PATH, "data", "logs", "edulink", cl.CurrentUser?.Username ?? cl.UserName ?? "nouser");
                if(!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                path = Path.Combine(path, $"{DateTime.Now:yyyy-MM-dd}.txt");
                var id = Guid.NewGuid();
                var chr = m.Source == "Request" ? '>' : '<';
                var pad = new string(chr, 3);
                string header = pad + id.ToString() + pad;
                string full = header + "\r\n" + m.Message + "\r\n" + new string(chr, header.Length) + "\r\n";
                if (m.Source == "Response")
                    full += "\r\n";
                File.AppendAllText(path, full);
                Program.LogMsg($"Logged as {id}", Discord.LogSeverity.Verbose, "EL:" + m.Source);
                return;
            }
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
