using Discord;
using Discord.Interactions;
using DiscordBot.Services.arr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Modules
{
    [DontAutoRegister]
    public class WatcherModule : BotSlashBase
    {
        public WatcherService Service { get; set; }

        [SlashCommand("watch", "Remember progress on a movie or show")]
        public async Task Watch(string url, string progress)
        {
            await RespondAsync("Fetching information");
            var info = await Service.GetItemInfo(Service.GetApiKeyFromUrl(url), Service.GetItemIdFromUrl(url));
            EmbedBuilder builder ;
            if(info is WatcherService.JellyfinEpisodeItem ep)
            {
                builder = Service.GetEmbed(url, $"{ep.SeriesName} S{ep.ParentIndexNumber:00}E{ep.IndexNumber:00}", progress);
            }
            else if(info is WatcherService.JellyfinMovieItem movie)
            {
                builder = Service.GetEmbed(url, $"{movie.Name} ({movie.ProductionYear})", progress);
            } else
            {
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Content = ":x: Unknown URL.";
                });
                return;
            }

            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = null;
                x.Embeds = new[] { builder.Build() };
                x.Components = Service.GetComponents().Build();
            });
        }
    }
}
