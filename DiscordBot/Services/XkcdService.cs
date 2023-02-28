using Discord;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class XkcdService : SavedService
    {
        class saveInfo
        {
            [JsonProperty("ch")]
            public Dictionary<ulong, ulong> channels = new Dictionary<ulong, ulong>();
            [JsonProperty("lc")]
            public int latestComic = 2323;
        }
        public Dictionary<ulong, ulong> Channels { get; set; }
        public int LatestComic { get; set; }
        public string JsonUrl = "https://xkcd.com/{0}/info.0.json";
        public string NormalUrl = "https://xkcd.com/{0}";
        public override void OnReady(IServiceProvider services)
        {
            var save = JsonConvert.DeserializeObject<saveInfo>(ReadSave());
            Channels = save.channels;
            LatestComic = save.latestComic;
        }

#if !DEBUG
        public override void OnLoaded() => OnDailyTick();
#endif

        public class XkcdInfo
        {
            public int Day { get; set; }
            public int Month { get; set; }
            public int Year { get; set; }

            [JsonProperty("num")]
            public int Number { get; set; }

            public string Link { get; set; }

            [JsonProperty("img")]
            public string ImageUrl { get; set; }
            [JsonProperty("alt")]
            public string AltText { get; set; }

            public string Title { get; set; }
        }

        async Task<XkcdInfo> getkNextComic()
        {
            var url = string.Format(JsonUrl, LatestComic + 1);
            var client = Program.GlobalServices.GetRequiredService<Classes.BotHttpClient>();
            var result = await client.GetAsync(url);
            if (!result.IsSuccessStatusCode)
                return null;
            var content = await result.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<XkcdInfo>(content);
        }

        public override void OnDailyTick()
        {
            var nextComic = getkNextComic().Result;
            if (nextComic == null)
                return;
            var embed = new EmbedBuilder()
                .WithDescription(nextComic.AltText ?? "No alt.")
                .WithImageUrl(nextComic.ImageUrl)
                .WithTitle($"#{nextComic.Number} {nextComic.Title}")
                .WithUrl(string.Format(NormalUrl, nextComic.Number))
                .Build();
            List<ulong> removeGuilds = new List<ulong>();
            foreach(var pair in Channels)
            {
                var guild = Program.Client.GetGuild(pair.Key);
                if(guild == null)
                {
                    removeGuilds.Add(pair.Key);
                    continue;
                }
                var channel = guild.GetTextChannel(pair.Value);
                if(channel == null)
                {
                    var chnl = guild.SystemChannel ?? guild.PublicUpdatesChannel ?? guild.DefaultChannel;
                    if(chnl != null && chnl is ITextChannel c)
                    {
                        c.SendMessageAsync("Channel no longer available for Xkcd messages, disabling.");
                        removeGuilds.Add(pair.Key);
                    }
                    continue;
                }
                channel.SendMessageAsync(embed: embed);
            }
            foreach (var x in removeGuilds)
                Channels.Remove(x);
            if (removeGuilds.Count > 0 || LatestComic != nextComic.Number)
            {
                LatestComic = nextComic.Number;
                this.OnSave();
            }
        }

        public override string GenerateSave()
        {
            var save = new saveInfo()
            {
                channels = Channels,
                latestComic = LatestComic
            };
            return JsonConvert.SerializeObject(save);
        }
    }
}
