using Discord;
using Discord.WebSocket;
using DiscordBot.Classes.Cinema;
using DiscordBot.Classes.Cinema.Odeon;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class CinemaService : SavedService
    {
        public ConcurrentDictionary<ulong, FilmSelectProcess> Selections { get; set; } = new ConcurrentDictionary<ulong, FilmSelectProcess>();
        private Dictionary<string, ICinema> _cinemas = new Dictionary<string, ICinema>();

        public static DirectoryInfo GetCacheDirectory()
        {
            var dir = new DirectoryInfo(Path.Combine(Program.BASE_PATH, "data", "cache", "cinema"));
            if (!dir.Exists)
                dir.Create();
            return dir;
        }

        void purgeOldCache(DirectoryInfo info)
        {
            var now = DateTime.Now.Date;
            foreach(var cachedFile in info.EnumerateFiles("*.json"))
            {
                var date = DateTime.Parse(cachedFile.Name.Replace(cachedFile.Extension, ""));
                if(date < now)
                {
                    Program.LogInfo($"Purging {cachedFile.FullName} from cache", "CinemaService");
                    cachedFile.Delete();
                }
            }
            foreach (var dir in info.EnumerateDirectories())
                purgeOldCache(dir);
        }

        public override void OnReady()
        {
            foreach(var cinema in new ICinema[] {new OdeonCinema()})
            {
                _cinemas[cinema.Name.ToLower()] = cinema;
            }

            var cachedData = GetCacheDirectory();
            purgeOldCache(cachedData);

            Task.Run(startup);
            //PrintFilms().Wait();
        }

        async Task startup()
        {
            var sv = ReadSave("[]");
            var selects = Program.Deserialise<List<FilmSelectProcess>>(sv);
            foreach (var x in selects)
            {
                if(x.Message != null)
                {
                    Selections[x.Message.Id] = x;
                }
            }

            Program.Client.InteractionCreated += Client_InteractionCreated;
        }

        async Task Client_InteractionCreated(SocketInteraction arg)
        {
            if (!(arg is SocketMessageComponent component))
                return;
            if (!component.Data.CustomId.StartsWith("cin:"))
                return;
            var split = component.Data.CustomId.Split(':', '-');
            if (!ulong.TryParse(split[1], out var msgId))
                return;
            if (!Selections.TryGetValue(msgId, out var sel))
                return;

            await component.DeferAsync();

            if (!sel.Loaded)
                await sel.Load(this);

            var changed = await sel.ExecuteInteraction(this, component, split.Skip(2).ToArray());
            if(sel.Message == null)
            {
                Selections.Remove(msgId, out _);
                OnSave();
                return; // as .Update() below would cause issues
            }
            if(changed)
            {
                OnSave();
                await sel.Update();
            }
        }

        public override string GenerateSave()
        {
            var s = Selections.Values;
            return Program.Serialise(s.ToList());
        }

        public async Task PrintFilms()
        {
            var c = GetCinema("odeon");
            var x = await c.GetFilmsAsync(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(3));
            foreach(var film in x)
            {
                Console.WriteLine("> " + film.Title);
                foreach(var show in film.Showings)
                {
                    Console.WriteLine("    - " + show.Start.ToString());
                }
            }
            Console.WriteLine("Done.");
        }


        public ICinema GetCinema(string name)
        {
            if (_cinemas.TryGetValue(name.ToLower(), out var c))
                return c;
            return null;
        }

        public static async Task<List<AutocompleteResult>> GetAutocompleteResults(SocketAutocompleteInteraction interaction)
        {
            var srv = Program.Services.GetRequiredService<CinemaService>();
            var cinema = srv.GetCinema("odeon");
            if(!cinema.CanAutocomplete)
            {
                _ = Task.Run(async () => await cinema.GetFilmsAsync());
                return new List<AutocompleteResult>()
                {
                    new AutocompleteResult($"Fetching films: {cinema.DaysFetched}/14 days fetched", "null")
                };
            }
            var films = await cinema.GetFilmsAsync();
            var text = interaction.Data.Current.Value as string;

            var results = new List<AutocompleteResult>();

            foreach(var film in films.OrderBy(x => x.Title))
            {
                string s = $"{film.Title} ({film.Year})";
                if (s.Contains(text, StringComparison.OrdinalIgnoreCase))
                    results.Add(new AutocompleteResult(s, film.Id));
            }
            return results.Take(20).ToList();
        }
    
        public async Task Register(FilmSelectProcess process, IUser creator)
        {
            if(Selections.TryGetValue(process.Message.Id, out var x))
            {
                try { await x.DeleteAsync(); } catch { }
            }
            Selections[process.Message.Id] = process;
            await process.NewInterested(creator);
            await process.Update();
            OnSave();
        }
    }
}
