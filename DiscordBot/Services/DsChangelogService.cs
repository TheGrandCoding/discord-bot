using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class DsChangelogService : SavedClassService<DsChangelogSave>, BuiltIn.IRegisterable
    {
        public override void OnDailyTick()
        {
            Task.Run(async () =>
            {
                try
                {
                    await execute();
                }
                catch (Exception ex)
                {
                    Error(ex);
                }
            });
        }
#if DEBUG
        public override void OnReady(IServiceProvider services)
        {
            base.OnReady(services);
            OnDailyTick();
        }
#endif

        public Task<string> RegisterAsync(IMessageChannel channel, IUser user)
        {
            if(!Data.ChannelIds.Contains(channel.Id))
            {
                Data.ChannelIds.Add(channel.Id);
                OnSave();
            }
            return Task.FromResult("That channel has been registered to receieve Discord changelogs");
        }
        public Task<string> UnregisterAsync(IMessageChannel channel, IUser user)
        {
            if(Data.ChannelIds.Remove(channel.Id))
            {
                OnSave();
            }
            return Task.FromResult("This channel was removed from receiving Discord changelogs");
        }


        const string baseUrl = "https://cdn.discordapp.com/changelogs/";
        async Task<Dictionary<ulong, int>> getMinVersions(BotHttpClient http, ChangelogPlatform platform)
        {
            var response = await http.GetAsync(baseUrl + $"config_{(int)platform}.json");
            await response.ThrowWithContentIfError();
            var dict = new Dictionary<ulong, int>();
            var content = await response.Content.ReadAsStringAsync();
            var jobj = JObject.Parse(content);
            foreach(var token in jobj.Children())
            {
                if(token is JProperty property)
                {
                    var data = (property.Value as JObject).GetValue("min_version");
                    dict[ulong.Parse(property.Name)] = data.ToObject<int>();
                }
            }
            return dict;
        }
        async Task<Changelog> fetchChangelog(BotHttpClient http, ChangelogPlatform platform, ulong entryId, string locale)
        {
            var response = await http.GetAsync(baseUrl + $"{(int)platform}/{entryId}/{locale}.json");
            await response.ThrowWithContentIfError();
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Changelog>(content);
        }
        async Task sendChangelog(Changelog log)
        {
            if(log.Content.Length > 4000)
            { // too big for embed, so upload
                var temp = Program.GetTempPath("changelog.md");
                await File.WriteAllTextAsync(temp, log.Content, Program.GetToken());
                foreach (var channelId in Data.ChannelIds)
                {
                    var channel = Program.Client.GetChannel(channelId);
                    if (channel != null && channel is IMessageChannel sendable)
                    {
                        await sendable.SendFileAsync(temp);
                    }
                }
                return;
            }
            var builder = new EmbedBuilder();

            var contentlines = log.Content.Split('\n');
            var lines = new List<string>();
            for(int i = 0; i < contentlines.Length; i++)
            {
                var line = contentlines[i];
                var chrs = line.Distinct().ToArray();
                if(chrs.Length == 1 && line.Length >= 3)
                { // header underline e.g. ==========
                    var text = contentlines[i - 1];
                    var idx = text.IndexOf('{');
                    if (idx != -1)
                    {
                        text = text.Substring(0, idx);
                    }
                    lines.RemoveAt(lines.Count - 1);
                    if(builder.Title == null)
                    {
                        builder.Title = text;
                    } else
                    {
                        lines.Add($"## {text}");
                    }
                    continue;
                }
                lines.Add(line);
            }
            builder.Description = string.Join('\n', lines);
            if (string.IsNullOrWhiteSpace(builder.Title))
                builder.Title = "Changelog";
            if (log.AssetType == 1)
                builder.WithImageUrl(log.Asset);
            var embed = builder.Build();

            foreach(var channelId in Data.ChannelIds)
            {
                var channel = Program.Client.GetChannel(channelId);
                if(channel != null && channel is IMessageChannel sendable)
                {
                    await sendable.SendMessageAsync(embed: embed);
                }
            }

        }
        public async Task execute()
        {
            if (!Data.ChannelIds.Any()) return;
            var http = Program.GlobalServices.GetRequiredService<BotHttpClient>()
                .Child("DsChangelog");
            var meta = await getMinVersions(http, ChangelogPlatform.Desktop);

            bool dirty = false;
            foreach((var entry_id, var min_version) in meta.OrderBy(x => x.Key))
            {
                if(entry_id > Data.LastChangelog)
                {
                    var changelog = await fetchChangelog(http, ChangelogPlatform.Desktop, entry_id, "en-US");

                    await sendChangelog(changelog);

                    Data.LastChangelog = entry_id;
                    dirty = true;
                }
            }
            if (dirty) OnSave();
        }


        enum ChangelogPlatform
        {
            Desktop = 0,
            Mobile = 1
        }
        class Changelog
        {
            [JsonProperty("changelog_id")]
            public ulong ChangelogId { get; set; }
            [JsonProperty("entry_id")]
            public ulong EntryId { get; set; }
            public string Locale { get; set; }
            public DateTime Date { get; set; }
            public string Asset { get; set; }
            [JsonProperty("asset_type")]
            public int AssetType { get; set; }
            public string Content { get; set; }
        }
    }

    public class DsChangelogSave
    {
        [JsonProperty("channels")]
        public List<ulong> ChannelIds { get; set; } = new();
        [JsonProperty("last_id")]
        public ulong LastChangelog { get; set; }
    }
}
