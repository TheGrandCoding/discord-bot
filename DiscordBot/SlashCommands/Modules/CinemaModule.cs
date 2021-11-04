using Discord;
using Discord.SlashCommands;
using DiscordBot.Classes.Cinema;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [CommandGroup("cinema", "Commands related to cinema things")]
    public class CinemaModule : BotSlashBase
    {
        public CinemaService Service { get; set; }

        [SlashCommand("find", "Finds ")]
        public async Task StartProcess(
            [Required]
            [Choice("Odeon", "odeon")]
            [ParameterName("cinema")]
            string cinemaId,
            [Required]
            [Autocomplete]
            [ParameterName("film")]
            string filmId)
        {
            if(string.IsNullOrWhiteSpace(filmId) || filmId == "null")
            {
                await Interaction.RespondAsync(":x: You must select an available film from the list of films", 
                    ephemeral: true);
                return;
            }
            var cinema = Service.GetCinema(cinemaId);
            if(cinema == null)
            {
                await Interaction.RespondAsync(":x: Invalid or unknown cinema, please contact the developer",
                    ephemeral: true);
                return;
            }

            await Interaction.DeferAsync(true);
            var film = await cinema.GetFilmAsync(filmId);
            if(film == null)
            {
                await Interaction.FollowupAsync(":x: Could not find that film", ephemeral: true);
                return;
            }

            var msg = await Interaction.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithDescription("This message will be updated shortly.").Build());


            var process = new FilmSelectProcess(cinema, film, Interaction.User as IGuildUser, msg);

            await Service.Register(process, Interaction.User);
            await Interaction.FollowupAsync("Starting time selection process!\n" + // says below, since we deferred above
                "Those interested, including you, should click the button in the message that appears below\n" +
                "After, they will receive a direct message from this bot with further instructions");


        }
    }
}
