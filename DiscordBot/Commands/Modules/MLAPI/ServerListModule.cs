using Discord.Commands;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules.MLAPI
{
    [Name("Master List")]
    [RequireOwner]
    [Group("masterlist"), Alias("ml")]
    public class ServerListModule : BotBase
    {
        public MLService Service { get; set; }

        [Command("rename")]
        [Summary("Renames a given server")]
        public async Task<RuntimeResult> Rename(Guid id, [Remainder]string newName)
        {
            if (!Service.Servers.TryGetValue(id, out var server))
                return new BotResult("No server exists by that id");
            server.Name = newName;
            Service.OnSave();
            await ReplyAsync("Renamed.");
            return new BotResult();
        }

        [Command("remove"), Alias("delete")]
        [Summary("Removes a given server")]
        public async Task<RuntimeResult> Delete(Guid id)
        {
            if (!Service.Servers.Remove(id))
                return new BotResult("No server exists by that id");
            await ReplyAsync("Removed.");
            Service.OnSave();
            return new BotResult();
        }

        [Command("purge")]
        [Summary("Removes all servers with a given type")]
        public async Task<RuntimeResult> Purge(string type)
        {
            var ls = Service.Servers
                .Where(x => x.Value.GameName == type)
                .Select(x => x.Key)
                .ToList();
            foreach (var key in ls)
                Service.Servers.Remove(key);
            if (ls.Count == 0)
                return new BotResult("No servers existed by that type");
            await ReplyAsync("Removed.");
            Service.OnSave();
            return new BotResult();
        }
    }
}
