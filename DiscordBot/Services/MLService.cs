using DiscordBot.Classes.ServerList;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DiscordBot.Services
{
    public class MLService : SavedService
    {
        public Dictionary<Guid, Server> Servers { get; set; }

        public Semaphore Lock { get; set; }

        public override string GenerateSave()
        {
            return Program.Serialise(Servers);
        }
        public override void OnReady()
        {
            Lock = new Semaphore(1, 1);
            var content = ReadSave();
            Servers = Program.Deserialise<Dictionary<Guid, Server>>(content);
        }
    }
}
