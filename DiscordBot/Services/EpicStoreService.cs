using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Services
{
    public class EpicStoreService : SavedService
    {
        public Dictionary<ulong, ulong> Channels { get; set; } = new Dictionary<ulong, ulong>();
        public Dictionary<string, DateTime> Games { get; set; } = new Dictionary<string, DateTime>();
        public override string GenerateSave()
        {
            var sv = new EpicSave();
            sv.channels = Channels;
            sv.games = Games;
            return Program.Serialise(sv);
        }
        public override void OnReady()
        {
            var sv = Program.Deserialise<EpicSave>(ReadSave());
            Channels = sv.channels ?? new Dictionary<ulong, ulong>();
            Games = sv.games ?? new Dictionary<string, DateTime>();
        }

    }
    class EpicSave
    {
        public Dictionary<ulong, ulong> channels { get; set; } = new Dictionary<ulong, ulong>();
        public Dictionary<string, DateTime> games { get; set; } = new Dictionary<string, DateTime>();
    }
}
