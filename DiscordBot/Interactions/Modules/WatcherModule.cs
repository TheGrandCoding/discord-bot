using Discord;
using Discord.Interactions;
using DiscordBot.Services;
using DiscordBot.Services.arr;
using Microsoft.Extensions.DependencyInjection;
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
    
        [SlashCommand("timetrack", "Fetches time tracker information")]
        public async Task Search(string code)
        {
            await RespondAsync("Searching...", ephemeral: true);
            using var db = Program.Services.GetRequiredService<TimeTrackDb>();
            var video = db.GetVideo(Context.User.Id, code);
            if(video == null)
            {
                await ModifyOriginalResponseAsync(x => x.Content = $"Video of ID {code} does not exist in the database.");
            } else
            {
                await ModifyOriginalResponseAsync(x =>
                {
                    var ts = TimeSpan.FromSeconds(video.WatchedTime);
                    x.Content = $"Video {code} information:\r\nTime: {video.WatchedTime}s ({ts:hh:mm:ss})\r\n" +
                    $"Last updated: {video.LastUpdated}";
                });
            }
        }
    }
}
