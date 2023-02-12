using Discord;
using Discord.Interactions;
using DiscordBot.Services;
using DiscordBot.Services.Radarr;
using DiscordBot.Services.Sonarr;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Modules
{
    [Group("trakt", "Commands for Trakt")]
    [RequireContext(ContextType.Guild)]
    public class TraktModule : BotSlashBase
    {
        public TraktService Service { get; set; }

        [SlashCommand("where", "Sees where reminders are sent")]
        public async Task Where()
        {
            if (!Service.Users.TryGetValue(Context.User.Id, out var save) || save.AccessToken == null)
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
    
        [SlashCommand("autocollect", "Automatically collect added things")]
        public async Task AutoCollect()
        {

            if (!Service.Users.TryGetValue(Context.User.Id, out var save) || save.AccessToken == null)
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
                    output.AppendLine($"+ {show.Title}: All {show.EpisodeCount} episodes");
                    continue;
                }

                List<SonarrEpisode> _episodes = null;
                Func<int, Task<IEnumerable<SonarrEpisode>>> getEpisodes = async (int seasonNum) => {
                    if (_episodes == null)
                        _episodes = await sonarr.GetAllEpisodesAsync(show.Id);
                    return _episodes.Where(x => x.SeasonNumber == seasonNum);
                };
                bool add = false;
                output.AppendLine($"+ {show.Title}:");
                foreach (var season in show.Seasons)
                {
                    var existingSeason = existing?.Seasons.FirstOrDefault(x => x.Number == season.SeasonNumber) ?? null;
                    if(season.Statistics.EpisodeCount == (existingSeason?.Episodes.Count ?? 0))
                    { // already synced all
                        continue;
                    }

                    var syncSeason = new TraktSeasonCollectInfo()
                    {
                        Number = season.SeasonNumber
                    };
                    var seasonEpisodes = await getEpisodes(season.SeasonNumber);
                    if (seasonEpisodes.All(x => x.HasFile))
                    { // downloaded this whole season
                        output.AppendLine($"-+ Season {season.SeasonNumber}");
                    }
                    else
                    { // downloaded only some episodes, let's see which ones
                        output.AppendLine($"-+ Season {season.SeasonNumber}:");
                        foreach(var x in seasonEpisodes.Where(x => x.HasFile))
                        {
                            output.AppendLine($"---+ Episode {x.EpisodeNumber}");
                            syncSeason.Episodes.Add(new TraktEpisodeCollectInfo()
                            {
                                Number = x.EpisodeNumber
                            });
                        }
                    }
                    add = true;
                    syncShow.Seasons.Add(syncSeason);
                }
                if (add)
                {
                    sync.Shows.Add(syncShow);
                }
            }
        }

        async Task syncMovies(string token, TraktCollection sync, StringBuilder output)
        {
            var collectedAlready = await Service.GetCollectedMoviesAsync(token);
            var radarr = Program.Services.GetRequiredService<RadarrWebhookService>();
            var movies = await radarr.GetMovies();

            foreach(var movie in movies)
            {
                var collected = collectedAlready.FirstOrDefault(x => x.Ids.TmDBId == movie.TmdbId);
                if(collected == null)
                {
                    output.AppendLine($"+ {movie.Title} {movie.Year}");
                    sync.Movies.Add(TraktMovieCollectInfo.From(movie));
                }
            }
        }

        [SlashCommand("sync", "Force sync stuff")]
        [RequireOwner]
        public async Task Sync()
        {
            if (!Service.Users.TryGetValue(Context.User.Id, out var save) || save.AccessToken == null)
            {
                await RespondAsync($"You must first authorize Trakt via the following link:\r\n<{Service.OAuthUri}>",
                    ephemeral: true);
                return;
            }
            await RespondAsync("Calculating necessary data...", ephemeral: true);
            string token;
            try
            {
                token = await save.AccessToken.GetToken(Service);
            }
            catch (HttpRequestException ex)
            {
                save.AccessToken = null;
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Content = $":x: Access token could not be fetched, please try authorizing again:\r\n{Service.OAuthUri}";
                });
                Program.LogInfo($"Token error", "TraktSync", ex);
                return;
            }
            var sb = new StringBuilder();
            var sync = new TraktCollection()
            {
                Shows = new List<TraktShowCollectInfo>(),
                Movies = new List<TraktMovieCollectInfo>()
            };
            await syncShows(token, sync, sb);
            //await syncMovies(token, sync, sb);
            string response = $"Syncing with Trakt..." +
                $"\r\nShows: {sync.Shows.Count}" +
                $"\r\nMovies: {sync.Movies.Count}";
            await ModifyOriginalResponseAsync(x => x.Content = response);

            var s = JsonConvert.SerializeObject(sync);
            var resp = await Service.SendCollectionAsync(token, s);
            Program.LogInfo(resp, "TraktSync-R");
            await ModifyOriginalResponseAsync(x => x.Content = response += "\r\nDone.");
            var temp = Path.Join(Path.GetTempPath(), "output.txt");
            await File.WriteAllTextAsync(temp, sb.ToString());
            await FollowupWithFileAsync(temp, ephemeral: true);

            File.Delete(temp);
        }
    }
}
