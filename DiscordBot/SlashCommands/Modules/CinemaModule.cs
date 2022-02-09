using Discord;
using Discord.Interactions;
using DiscordBot.Classes.Cinema;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [Group("cinema", "Commands related to cinema things")]
    public class CinemaModule : BotSlashBase
    {
        public CinemaService Service { get; set; }

        [SlashCommand("find", "Finds ")]
        public async Task StartProcess(
            [Choice("Odeon", "odeon")]
            string cinemaId,
            [Autocomplete]
            string filmId)
        {
            if(string.IsNullOrWhiteSpace(filmId) || filmId == "null")
            {
                await RespondAsync(":x: You must select an available film from the list of films", 
                    ephemeral: true);
                return;
            }
            var cinema = Service.GetCinema(cinemaId);
            if(cinema == null)
            {
                await RespondAsync(":x: Invalid or unknown cinema, please contact the developer",
                    ephemeral: true);
                return;
            }

            await DeferAsync(true);
            var film = await cinema.GetFilmAsync(filmId);
            if(film == null)
            {
                await FollowupAsync(":x: Could not find that film", ephemeral: true);
                return;
            }

            var msg = await Context.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithDescription("This message will be updated shortly.").Build());


            var process = new FilmSelectProcess(cinema, film, Context.User as IGuildUser, msg);

            await Service.Register(process, Context.User);
            await FollowupAsync("Starting time selection process!\n" + // says below, since we deferred above
                "Those interested, including you, should click the button in the message that appears below\n" +
                "After, they will receive a direct message from this bot with further instructions");


        }
    
        [SlashCommand("odeon_jwt", "Set auth token for Odeon")]
        public async Task SetToken(string jwt)
        {
            var cin = Service.GetCinema("odeon") as Classes.Cinema.Odeon.OdeonCinema;
            cin.SetToken(jwt);
            await RespondAsync("Set.", ephemeral: true);
        }
    }

    public class FilmIdAutocomplete : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            var srv = Program.Services.GetRequiredService<CinemaService>();
            var cinema = srv.GetCinema("odeon");
            if (!cinema.CanAutocomplete)
            {
                _ = Task.Run(async () => await cinema.GetFilmsAsync());
                return AutocompletionResult.FromSuccess(new List<AutocompleteResult>()
                {
                    new AutocompleteResult($"Fetching films: {cinema.DaysFetched}/14 days fetched", "null")
                });
            }
            var films = await cinema.GetFilmsAsync();
            var text = autocompleteInteraction.Data.Current.Value as string;

            var results = new List<AutocompleteResult>();

            foreach (var film in films.OrderBy(x => x.Title))
            {
                string s = $"{film.Title} ({film.Year})";
                if (s.Contains(text, StringComparison.OrdinalIgnoreCase))
                    results.Add(new AutocompleteResult(s, film.Id));
            }
            return AutocompletionResult.FromSuccess(results.Take(20));
        }
    }
}
