using CodeHollow.FeedReader;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class JackettService : Service
    {
        string getUrl(string site, string categories, string query)
        {
            var baseUrl = Program.Configuration["urls:jackett"];
            var apikey = Program.Configuration["tokens:jackett"];
            return baseUrl + $"api/v2.0/indexers/{site}/results/torznab/api?apikey={apikey}&t=search&cat={categories}&q={query}";
        }

        public async Task<FeedItem[]> SearchAsync(string site, string text, TorrentCategory[] categories)
        {
            var url = getUrl(site, string.Join(",", categories.Select(x => (int)x)), Uri.EscapeDataString(text));
            var feed = await FeedReader.ReadAsync(url);
            return feed.Items.ToArray();
        }


        public enum TorrentCategory
        {
            Movies = 2000,
            Movies_SD = 2030,
            Movies_HD = 2040,
            Movies_Bluray = 2050,

            PC_Games = 4050,

            TV = 5000,
            TV_SD = 5030,
            TV_HD = 5040
        }

        public enum TorrentOrderBy
        {
            Ratio = 0,
            Time,
            Seeds,
            Leechers,


        }
    }
}
