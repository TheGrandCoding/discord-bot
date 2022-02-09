using Discord;
using Discord.WebSocket;
using DiscordBot.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DiscordBot.Utils.EnumerableUtils;

namespace DiscordBot.Classes.Cinema
{
    public class FilmSelectProcess
    {
        [JsonConstructor]
        private FilmSelectProcess(string cinema, string film)
        {
            _cinema = cinema;
            _film = film;
        }

        public FilmSelectProcess(ICinema cinema, IFilm film, IGuildUser creator, IUserMessage message)
        {
            Cinema = cinema;
            Film = film;
            Message = message;
            User = creator;
        }

        [JsonIgnore]
        public ICinema Cinema { get; set; }
        [JsonIgnore]
        public IFilm Film { get; set; }


        private string _cinema;
        private string _film;

        [JsonProperty("cinema")]
        private string cinemaId => _cinema ?? Cinema.Name;
        [JsonProperty("film")]
        private string filmId => _film ?? Film.Id;

        public IUserMessage Message { get; set; }

        public IGuildUser User { get; set; }

        [JsonIgnore]
        public string IdPrefix => $"cin:{Message.Id}";

        public bool Collapsed { get; set; } = false;

        [JsonProperty("users")]
        public Dictionary<ulong, UserSelectedFilm> UserPreferences { get; set; } = new Dictionary<ulong, UserSelectedFilm>();

        [JsonIgnore]
        public bool Loaded { get; set; }
        public async Task Load(CinemaService srv)
        {
            Loaded = true;
            Cinema = srv.GetCinema(cinemaId);
            Film = await Cinema.GetFilmAsync(filmId);
        }

        public async Task NewInterested(IUser user)
        {
            if(UserPreferences.TryGetValue(user.Id, out var x))
            {
                try { await x.DM.DeleteAsync(); } catch { }
            }
            var msg = await user.SendMessageAsync(embed: new EmbedBuilder()
                .WithDescription("This message will be updated shortly").Build());
            var pref = new UserSelectedFilm();
            pref.DM = msg;
            await pref.Update(Film, IdPrefix + $":{user.Id}");

            UserPreferences[user.Id] = pref;
        }

        public void SetPreference(ulong userId, IShowing showing, bool availability)
        {
            if(!UserPreferences.TryGetValue(userId, out var us))
            {
                us = new UserSelectedFilm();
                UserPreferences[userId] = us;
            }
            us.Add(showing, availability);
        }
        public bool GetPreference(ulong userId, IShowing showing)
        {
            if (UserPreferences.TryGetValue(userId, out var b))
                return b.Get(showing);
            return false;
        }
    
        public int GetCountFor(IShowing showing)
        {
            return UserPreferences.Values.Sum(x => x.Get(showing) ? 1 : 0);
        }

        public EmbedBuilder ToPublicEmbed()
        {
            var builder = new EmbedBuilder();
            builder.Title = $"Viewings of {Program.Clamp(Film.Title, 64)} at {Cinema.Name}";
            builder.Description = $"{UserPreferences.Count} possible attendees\n" +
                (Collapsed ? $"Only showings with at least one possible attendee are being displayed" : "All showings are being shown, except those which are unavailable");

            var groups = Film.Showings.GroupBy(x => x.Start.Date);
            foreach (var dayGroup in groups)
            {
                var value = "";
                DateTime date = dayGroup.Key;
                foreach (var entry in dayGroup)
                {
                    if (entry.Expired)
                        continue; // start time passed
                    var sum = GetCountFor(entry);

                    if (Collapsed && sum == 0)
                        continue;

                    var row = $"{entry.Start:HH:mm}";
                    if (sum > 0)
                    {
                        var perc = (sum / (double)UserPreferences.Count) * 100;
                        row += $", {perc:00}%";
                        if(perc >= 80)
                        {
                            row = $"**{row}**";
                        }
                    }
                    if(entry.SoldOut)
                    {
                        if (sum == 0)
                            continue; // don't bother
                        row = $"~~{row}~~";
                    }
                    value += $"{row}\n";
                }
                if(!string.IsNullOrWhiteSpace(value))
                    builder.AddField($"{date:dddd, dd MMMM}", value, true);
                if (builder.Fields.Count >= 25)
                    break;
            }

            return builder;
        }

        public ComponentBuilder ToComponents(string idPrefix)
        {
            var builder = new ComponentBuilder();
            builder.WithButton("Join", idPrefix + "-int", emote: Emotes.HEAVY_PLUS_SIGN);

            builder.WithButton(Collapsed ? "Expand" : "Collapse", idPrefix + "-col", ButtonStyle.Secondary, emote: Emotes.MAG);

            builder.WithButton("Delete", idPrefix + "-del", ButtonStyle.Danger, emote: Emotes.BLACK_X);

            return builder;
        }

        public async Task Update(bool updateUsers = false)
        {
            await Message.ModifyAsync(x =>
            {
                x.Components = ToComponents(IdPrefix).Build();
                x.Embed = ToPublicEmbed().Build();
            });

            if(updateUsers)
            {
                foreach(var keypair in UserPreferences)
                {
                    await keypair.Value.Update(Film, IdPrefix + $":{keypair.Key}");
                }
            }
        }

        public async Task DeleteAsync()
        {
            await Message.DeleteAsync();
            Message = null;
            foreach(var usr in UserPreferences.Values)
            {
                try
                {
                    await usr.DeleteAsync();
                } catch { }
            }
        }

        public async Task<bool> ExecuteInteraction(CinemaService service, SocketMessageComponent interaction, string[] idShard)
        {
            if (ulong.TryParse(idShard[0], out var userId))
            {
                if (UserPreferences.TryGetValue(userId, out var us))
                {
                    var changesMade = await us.ExecuteInteraction(this, IdPrefix + $":{userId}", service, interaction, idShard.Skip(1).ToArray());
                    if(us.DM == null)
                    {
                        UserPreferences.Remove(userId);
                        changesMade = true;
                    }
                    return changesMade;
                }
            } else
            {
                if(idShard[0] == "int")
                {
                    try
                    {
                        await NewInterested(interaction.User);
                        await interaction.FollowupAsync("Done! Check your DMs with this bot", ephemeral: true);
                        return true;
                    }
                    catch (Exception e)
                    {
                        Program.LogError(e, "FilmSelectProcess");
                        await interaction.FollowupAsync("An error occured, make sure your DMs are open with the bot", ephemeral: true);
                    }
                } else if (idShard[0] == "col")
                {
                    Collapsed = !Collapsed;
                    await Update();
                } else if(idShard[0] == "del")
                {
                    if(interaction.User.Id != User.Id)
                    {
                        await interaction.FollowupAsync("Only the user who started this can delete it", ephemeral: true);
                    } else
                    {
                        await DeleteAsync();
                        return true;
                    }
                }
            }
            return false;
        }
    }

    public class UserSelectedFilm
    {
        [JsonProperty("times")]
        public Dictionary<string, bool> ShowingPreferences { get; set; } = new Dictionary<string, bool>(); 

        public DateTime? DateSelected { get; set; }

        public IUserMessage DM { get; set; }

        public void Add(IShowing showing, bool pref)
        {
            ShowingPreferences[showing.Id] = pref;
        }
        public bool Get(IShowing showing)
        {
            if (ShowingPreferences.TryGetValue(showing.Id, out var b))
                return b;
            return false;
        }
    
        EmbedBuilder ToEmbed(IFilm film)
        {
            var builder = new EmbedBuilder();
            builder.Description = $"Please select the times you would be available to see {film.Title}\n" +
                $"You can select as many, or as few, as you'd be able to view";

            if(ShowingPreferences.Count > 0)
            {
                builder.Description += "\nYou have indicated you can attend the following showings:";

                var grouped = new Dictionary<DateTime, List<string>>();
                var now = DateTime.Now;
                foreach(var key in ShowingPreferences.Keys)
                {
                    var spl = key.Split("-");
                    var date = new DateTime(now.Year, 1, 1).AddDays(int.Parse(spl[0]) - 1);
                    grouped.AddInner(date, string.Join(":", spl[1], spl[2]));
                }

                foreach((var key, var ls) in grouped)
                {
                    var val = "";
                    foreach (var x in ls)
                        val += x + "\n";
                    builder.AddField($"{key:dddd, dd MMMM}", val, true);
                }
            }

            return builder;
        }

        ComponentBuilder ToComponents(IFilm film, DateTime? dateSelected, string idPrefix)
        {
            var builder = new ComponentBuilder();

            var dayMenu = new SelectMenuBuilder();
            dayMenu.CustomId = $"{idPrefix}-day";
            dayMenu.MinValues = 1;
            dayMenu.MaxValues = 1;
            dayMenu.Placeholder = "Date to view times for";

            var timesMenu = new SelectMenuBuilder();
            timesMenu.CustomId = $"{idPrefix}-time";
            timesMenu.MinValues = 0;
            timesMenu.Placeholder = "Select any times available";

            var grouped = film.Showings.GroupBy(x => x.Start.Date);
            foreach(var dayGroup in grouped)
            {
                bool any = false;
                DateTime date = dayGroup.Key;
                foreach(var showing in dayGroup)
                {
                    if (showing.Expired)
                        continue;
                    any = any || Get(showing);
                    if(dateSelected.HasValue)
                    {
                        var val = dateSelected.Value;
                        if(val.Year == date.Year && val.DayOfYear == date.DayOfYear)
                        {
                            var hasSelected = Get(showing);
                            string desc = null;
                            if(showing.SoldOut)
                            {
                                if(hasSelected)
                                {
                                    desc = "This showing has soldout";
                                } else
                                {
                                    continue;
                                }
                            }
                            timesMenu.AddOption($"{showing.Start:hh:mm tt}", showing.Id, desc, isDefault: hasSelected);
                        }
                    }
                }

                IEmote emote = any ? Emotes.FAST_FORWARD : null;
                var def = dateSelected.HasValue ? dateSelected.Value.Date.Equals(date) : false;
                dayMenu.AddOption($"{date:dddd, dd MMMM}", $"{date:yyyy-MM-dd}", emote: emote, isDefault: def);
            }

            builder.WithSelectMenu(dayMenu);

            if (timesMenu.Options.Count > 0)
            {
                timesMenu.MaxValues = timesMenu.Options.Count;
                builder.WithSelectMenu(timesMenu, 1);
            }

            builder.WithButton("Cancel", idPrefix + "-del", ButtonStyle.Danger, row: 2);

            return builder;
        }
    
        public async Task Update(IFilm film, string idPrefix)
        {
            await DM.ModifyAsync(x =>
            {
                x.Embed = ToEmbed(film).Build();
                x.Components = ToComponents(film, DateSelected, idPrefix).Build();
            });
        }

        public async Task DeleteAsync()
        {
            await DM.DeleteAsync();
            ShowingPreferences = null;
            DM = null;
        }
    
        public async Task<bool> ExecuteInteraction(FilmSelectProcess process, string idPrefix, CinemaService service, SocketMessageComponent interaction, string[] idShard)
        {
            string action = idShard[0].ToString();
            if(action == "day")
            { // we're changing the day
                var value = interaction.Data.Values.First();
                var date = DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.CurrentCulture);
                DateSelected = date;
                await interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Embed = ToEmbed(process.Film).Build();
                    x.Components = ToComponents(process.Film, DateSelected, idPrefix).Build();
                });
            } else if(action == "time")
            {
                var time_ids = interaction.Data.Values;
                var dayofyear = DateSelected.GetValueOrDefault(DateTime.Now).DayOfYear.ToString();


                var allSameDates = ShowingPreferences.Keys.Where(x => x.Split("-")[0] == dayofyear).ToList();
                foreach (var x in allSameDates)
                    ShowingPreferences.Remove(x);
                foreach (var nw in time_ids)
                    ShowingPreferences[nw] = true;


                await interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Embed = ToEmbed(process.Film).Build();
                    x.Components = ToComponents(process.Film, DateSelected, idPrefix).Build();
                });
                return true;
            } else if(action == "del")
            {
                await DeleteAsync();
                return true;
            }
            return false;
        }

    }


}
