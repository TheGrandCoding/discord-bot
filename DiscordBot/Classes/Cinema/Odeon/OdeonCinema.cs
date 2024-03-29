﻿using Discord;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Classes.Cinema.Odeon
{
    public class OdeonCinema : ICinema
    {
        public string Name => "Odeon";
        public string Location => "Town";
        public bool CanAutocomplete => _filmCache != null && _filmCache.Expired == false;

        public static string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36";

        public static string Domain = "odeon.co.uk";

        void addDefaultHeaders(HttpRequestMessage request, bool json)
        {
            var dict = new Dictionary<string, string>()
            {
                {"accept",  json ? "application/json" : "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9"},
                {"accept-encoding", "gzip, deflate, br"},
                {"accept-language", "en-GB,en-US;q=0.9,en;q=0.8"},
                {"cache-control", "no-cache"},
                {"pragma", "no-cache"},
                {"sec-ch-ua", "\"Google Chrome\";v=\"95\", \"Chromium\";v=\"95\", \";Not A Brand\";v=\"99\""},
                {"sec-ch-ua-mobile", "?0"},
                {"sec-ch-ua-platform", "\"Windows\""},
                {"sec-fetch-dest", "document"},
                {"sec-fetch-mode", "navigate"},
                {"sec-fetch-site", "none"},
                {"sec-fetch-user", "?1"},
                {"upgrade-insecure-requests", "1"},
                {"user-agent", UserAgent }
            };
            foreach (var keypair in dict)
                request.Headers.Add(keypair.Key, keypair.Value);

        }

        private BotHttpClient http = Program.GlobalServices.GetRequiredService<BotHttpClient>()
            .Child("OdeonAPI", true, new BotHttpClient.DefaultRateLimiter(3000), true)
            .WithCookie(new Cookie("__cf_bm", "RArBUmKXB4nH73gDSb2mMmkRMFBwpW6iCu0OS3WzFZM-1636754503-0-AX/H6vRbvjsyA5FaEPFl5nvmWl0EzGEFMRPklVekTjgXheM7OfoJJjLHgjkDX3xGkva2ApsQziIWB+qrj+2yuto=", "/", Domain));
        private SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        FileInfo getCachePath(string fname)
        {
            var dir = new DirectoryInfo(Path.Combine(Services.CinemaService.GetCacheDirectory().FullName, "odeon"));

            if (!dir.Exists)
                dir.Create();

            return new FileInfo(Path.Combine(dir.FullName, fname));
        }

        public void SetToken(string jwt)
        {
            auth_jwt = jwt;
            triedWithAuth = false;
        }

        private string auth_jwt;
        static bool triedWithAuth = false;

        async Task<string> getJwtAsync()
        {
            if (triedWithAuth)
                return null; // don't keep spamming them
            var req = new HttpRequestMessage(HttpMethod.Get, $"https://{Domain}/cinemas/");
            addDefaultHeaders(req, false);
            var resp = await http.SendAsync(req);
            var content = await resp.Content.ReadAsStringAsync();

            // BotDbAuthToken is embedded in script, which is at the end of the HTML doc
            string BotDbAuthToken = null;
            int scriptEndIndex = content.Length;
            int scriptStartIndex = content.Length;
            do
            {
                scriptStartIndex = content.LastIndexOf("<script>", scriptStartIndex);
                scriptEndIndex = content.LastIndexOf("</script>", scriptEndIndex);

                if (scriptStartIndex == -1 || scriptEndIndex == -1)
                    break;
                var authT = content.IndexOf("\"BotDbAuthToken\"", scriptStartIndex, scriptEndIndex - scriptStartIndex);
                if (authT == -1)
                    continue;

                var tokenStarts = authT + "\"BotDbAuthToken\":\"".Length;
                var tokenEnds = content.IndexOf("\"", tokenStarts);

                return content[new Range(tokenStarts, tokenEnds)];
            } while (string.IsNullOrWhiteSpace(BotDbAuthToken) && scriptStartIndex >= 0);

            return null;
        }

        async Task<HttpResponseMessage> callWithAuth(HttpRequestMessage request)
        {
            bool retried = false;
startFunc:
            if (string.IsNullOrWhiteSpace(auth_jwt))
            {
                auth_jwt = await getJwtAsync();
                if(string.IsNullOrWhiteSpace(auth_jwt))
                {
                    if(!triedWithAuth)
                        await Program.AppInfo.Owner.SendMessageAsync($"Odeon has null auth token, please fix");
                    triedWithAuth = true;
                    throw new HttpRequestException("Auth token could not be gotten");
                }
                else
                {
                    triedWithAuth = false;
                    await Program.AppInfo.Owner.SendMessageAsync($"New auth token:\n```\n{auth_jwt}\n```");
                }
            }
            request.Headers.Add("authorization", "Bearer " + auth_jwt);
            addDefaultHeaders(request, true);
            try
            {
                var resp = await http.SendAsync(request);
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    triedWithAuth = true;
                    if (retried)
                        throw new HttpRequestException("Unauthorised access: " + await resp.Content.ReadAsStringAsync());
                    retried = true;
                    auth_jwt = null;
                    goto startFunc;
                }
                if(!resp.IsSuccessStatusCode)
                {
                    Program.LogWarning($"{await resp.Content.ReadAsStringAsync()}", "OdeonAPI");
                    resp.EnsureSuccessStatusCode(); // throws error
                }
                return resp;
            } catch(Exception e)
            {
                if(retried)
                {
                    throw;
                } else
                {
                    retried = true;
                    Program.LogWarning($"Waiting, then re-attempting due to exception {e.Message}", "OdeonAPI");
                    await Task.Delay(1000);
                    goto startFunc;
                }
            }
        }

        private Cached<IReadOnlyCollection<OdeonFilm>> _filmCache { get; set; }

        struct dayInfo
        {
            public Dictionary<string, ApiFilm> films;
            public Dictionary<string, List<OdeonShowing>> showtimes;

            public void Deconstruct(out Dictionary<string, ApiFilm> of, out Dictionary<string, List<OdeonShowing>> os)
            {
                of = films;
                os = showtimes;
            }
        }

        async Task<dayInfo> getFilmForDayAsync(DateTime date)
        {
            var path = getCachePath($"{date:yyyy-MM-dd}.json");
            string text;
            if(path.Exists)
            {
                text = File.ReadAllText(path.FullName, Encoding.UTF8);
                Program.LogInfo($"Loaded cache {path.Name}", "OdeonAPI");
            }
            else
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://vwc.{Domain}/WSVistaWebClient/ocapi/v1/browsing/master-data/showtimes/business-date/{date:yyyy-MM-dd}?siteIds=040");

                try
                {
                    var response = await callWithAuth(request);
                    text = await response.Content.ReadAsStringAsync();

                    File.WriteAllText(path.FullName, text, Encoding.UTF8);
                } catch(HttpRequestException e) when (e.Message == "Auth token could not be gotten")
                {
                    Program.LogError(e, "OdeonAPI");
                    return new dayInfo() { films = new Dictionary<string, ApiFilm>(), showtimes = new Dictionary<string, List<OdeonShowing>>() };
                }
            }

            var parsed = JsonConvert.DeserializeObject<ApiMasterData>(text);

            var films = new Dictionary<string, ApiFilm>();
            var showtimes = new Dictionary<string, List<OdeonShowing>>();
            foreach (var x in (parsed.relatedData.films ?? new ApiFilm[0]))
            {
                if (!films.ContainsKey(x.id))
                    films[x.id] = x;
            }
            foreach (var showtime in (parsed.showtimes ?? new List<ApiShowtime>()))
            {
                showtimes.AddInner(showtime.filmId, OdeonShowing.Create(showtime));
            }
            return new dayInfo() { films = films, showtimes = showtimes };
        }

        public int DaysFetched => daysFetched.Count;
        Dictionary<int, bool> daysFetched = new Dictionary<int, bool>();
        public async Task<IReadOnlyCollection<OdeonFilm>> GetFilmsAsync(DateTimeOffset startDate, DateTimeOffset endDate)
        {
            var films = new Dictionary<string, ApiFilm>();
            var showtimes = new Dictionary<string, List<OdeonShowing>>();
            var date = startDate.Date;
            while(date < endDate)
            {
                (var dayFilm, var dayShows) = await getFilmForDayAsync(date);
                daysFetched[date.DayOfYear] = true;
                foreach(var x in dayFilm.Values)
                {
                    if (!films.ContainsKey(x.id))
                        films[x.id] = x;
                }
                foreach (var show in dayShows)
                    foreach(var item in show.Value)
                        showtimes.AddInner(show.Key, item);

                date = date.AddDays(1);
            }

            var ls = new List<OdeonFilm>();
            foreach(var apiFilm in films)
            {
                var filmShowings = showtimes.GetValueOrDefault(apiFilm.Key, new List<OdeonShowing>());
                var movie = OdeonFilm.Create(apiFilm.Value, filmShowings);
                ls.Add(movie);
            }

            return ls;
        }
    
        public async Task<IReadOnlyCollection<OdeonFilm>> GetFilmsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (!CanAutocomplete)
                {
                    var films = await GetFilmsAsync(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(14));
                    _filmCache = new Cached<IReadOnlyCollection<OdeonFilm>>(films, 60);
                }
                return _filmCache.Value;
            } finally
            {
                _lock.Release();
            }
        }

        public async Task<OdeonFilm> GetFilmAsync(string id)
        {
            var films = await GetFilmsAsync();
            var desired = films.FirstOrDefault(x => x.Id == id);
            return desired;
        }


        // IFilm
        async Task<IReadOnlyCollection<IFilm>> ICinema.GetFilmsAsync(DateTimeOffset startDate, DateTimeOffset endDate)
            => (await GetFilmsAsync(startDate, endDate)).Cast<IFilm>().ToArray();
        async Task<IReadOnlyCollection<IFilm>> ICinema.GetFilmsAsync()
            => (await GetFilmsAsync()).Cast<IFilm>().ToArray();
        async Task<IFilm> ICinema.GetFilmAsync(string id) => (await GetFilmAsync(id)) as IFilm;
    }
}
