using Discord;
using Discord.Interactions;
using DiscordBot.Services;
using DiscordBot.Services.Radarr;
using DiscordBot.Services.Sonarr;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Modules
{
    [Group("trakt", "Commands for Trakt")]
    [RequireContext(ContextType.Guild)]
    public class TraktModule : BotSlashBase
    {
        public TraktService Service { get; set; }
        [SlashCommand("register", "Sends calendar reminders in this channel")]
        public async Task Register()
        {
            if(!(Context.Channel is ITextChannel txt))
            {
                await RespondAsync($":x: This must be ran in a server text channel.", ephemeral: true);
                return;
            }
            if(!Service.Users.TryGetValue(Context.User.Id, out var save))
            {
                await RespondAsync($":information_source: You must first authorize Trakt via the following link:\r\n<{Service.OAuthUri}>",
                    ephemeral: true);
                return;
            }
            save.Channel = Context.Channel as ITextChannel;
            Service.OnSave();
            await RespondAsync($"Success!\r\nThis channel will now receive messages every day for episodes airing that day.");
        }

        [SlashCommand("where", "Sees where reminders are sent")]
        public async Task Where()
        {
            if (!Service.Users.TryGetValue(Context.User.Id, out var save))
            {
                await RespondAsync($"Reminders are being sent nowhere. You must first authorize Trakt via the following link:\r\n<{Service.OAuthUri}>",
                    ephemeral: true);
                return;
            }
            if(save.Channel == null)
            {
                await RespondAsync($"Reminders are being sent nowhere. You must use the `/trakt register` command in the channel you want.", ephemeral: true);
                return;
            }
            await RespondAsync($"Reminders are being sent in {save.Channel.Mention}", ephemeral: true);
        }
        [SlashCommand("remove", "Removes yourself from reminders")]
        public async Task Remove()
        {
            if (Service.Users.Remove(Context.User.Id))
            {
                Service.OnSave();
                await RespondAsync($"You have been removed from our records.", ephemeral: true);
            }
            else
            {
                await RespondAsync($"You were not in our records to begin with", ephemeral: true);
            }
        }
    
        [SlashCommand("autocollect", "Automatically collect added things")]
        public async Task AutoCollect()
        {

            if (!Service.Users.TryGetValue(Context.User.Id, out var save))
            {
                await RespondAsync($"You must first authorize Trakt via the following link:\r\n<{Service.OAuthUri}>",
                    ephemeral: true);
                return;
            }
            save.AutoCollect = !save.AutoCollect;
            Service.OnSave();
            await RespondAsync(save.AutoCollect ? "Autocollect is now enabled" : "Autocollect is now disabled", ephemeral: true);
        }

        async Task syncShows(string token, TraktCollection sync, StringBuilder output)
        {
            var collectedAlready = await Service.GetCollectedShowsAsync(token);
            var sonarr = Program.Services.GetRequiredService<SonarrWebhooksService>();
            var shows = await sonarr.GetShows();

            foreach(var show in shows)
            {
                var syncShow = TraktShowCollectInfo.From(show);
                syncShow.CollectedAt = show.Added;
                var existing = collectedAlready.FirstOrDefault(x => x.Ids.TvDBId == show.TvDbId);

                if(show.EpisodeCount == (existing?.EpisodeCount ?? 0))
                { // we're already up to date.
                    continue;
                }
                if(show.EpisodeCount == show.TotalEpisodeCount)
                {
                    sync.Shows.Add(syncShow);
                    output.AppendLine($"Adding full show {show.Title}, with {show.EpisodeCount} episodes");
                    continue;
                }
                bool add = false;
                foreach(var season in show.Seasons)
                {
                    var existingSeason = existing?.Seasons.FirstOrDefault(x => x.Number == season.SeasonNumber) ?? null;
                    if(season.Statistics.EpisodeCount == (existingSeason?.Episodes.Count ?? 0))
                    { // already synced all
                        continue;
                    }
                    if(season.Statistics.EpisodeCount == season.Statistics.TotalEpisodeCount)
                    { // downloaded this whole season
                        var syncSeason = new TraktSeasonCollectInfo()
                        {
                            Number = season.SeasonNumber
                        };
                        output.AppendLine($"Adding Season {season.SeasonNumber} of {show.Title}");
                        syncShow.Seasons.Add(syncSeason);
                        add = true;
                    }
                }
                if (add)
                {
                    sync.Shows.Add(syncShow);
                }
            }
        }

        [SlashCommand("sync", "Force sync stuff")]
        [RequireOwner]
        public async Task Sync()
        {
            if (!Service.Users.TryGetValue(Context.User.Id, out var save))
            {
                await RespondAsync($"You must first authorize Trakt via the following link:\r\n<{Service.OAuthUri}>",
                    ephemeral: true);
                return;
            }
            await RespondAsync("Calculating necessary data...", ephemeral: true);
            var token = await save.AccessToken.GetToken(Service);
            var sb = new StringBuilder();
            var sync = new TraktCollection()
            {
                Shows = new List<TraktShowCollectInfo>(),
                Movies = new List<TraktMovieCollectInfo>()
            };
            await syncShows(token, sync, sb);
            Program.LogInfo(sb.ToString(), "TraktSync");
            await ModifyOriginalResponseAsync(x => x.Content = $"Syncing with Trakt...\r\nShows: {sync.Shows.Count}");
            var s = JsonConvert.SerializeObject(sync);
            var resp = await Service.SendCollectionAsync(token, s);
            Program.LogInfo(resp, "TraktSync-R");
            await ModifyOriginalResponseAsync(x => x.Content = $"Done");
        }
    }
}
