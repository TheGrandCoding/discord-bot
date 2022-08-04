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
            WatcherService.JellyfinItem info;
            var auth = await WatcherService.JellyfinAuth.Parse(url, Context.User.Id, Service);
            if(string.IsNullOrWhiteSpace(auth.UserId))
            {
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Content = $":x: Jellyfin User Id could not be parsed or fetched from your input.";
                });
                return;
            }
            if(url.Contains("/Download"))
            {
                info = await Service.GetItemInfo(Service.GetItemIdFromUrl(url), auth);
            } else
            {
                info = await Service.GetItemInfo(url, auth);
            }
            EmbedBuilder builder ;
            bool hasNext = false;
            if(info is WatcherService.JellyfinEpisodeItem ep)
            {
                builder = await ep.ToEmbed(Service, auth);
                hasNext = true;
            } else if(info is WatcherService.JellyfinPlaylist pl)
            {
                builder = await pl.ToEmbed(Service, auth);
                hasNext = true;
                progress = null;
            }
            else if(info is WatcherService.JellyfinMovieItem movie)
            {
                builder = await movie.ToEmbed(Service, auth);
            } else
            {
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Content = ":x: Unknown URL.";
                });
                return;
            }
            if(progress != null)
                builder.Description = (builder.Description ?? "") + $"**{progress}**";

            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = null;
                x.Embeds = new[] { builder.Build() };
                x.Components = Service.GetComponents(hasNext).Build();
            });
        }
    
        [SlashCommand("timetrack", "Fetches time information")]
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
                    x.Content = $"Video {code} information:\r\nTime: {video.WatchedTime}s ({ts:hh\\:mm\\:ss})\r\n" +
                    $"Last updated: {video.LastUpdated}";
                });
            }
        }
    }
}
