using Discord;
using DiscordBot.Services.BuiltIn;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace DiscordBot.Services
{
    public class LottoService : SavedClassService<LottoSave>, IRegisterableOption
    {
        [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
        class Lotto
        {
            public Lotto(string slug, string name, int maindrawn, int mainopts, int luckydrawn = 0, int luckyopts = 0, bool exclusive = false)
            {
                Slug = slug;
                Name = name;
                MainDrawn = maindrawn;
                MainOptions = mainopts;
                LuckyDrawn = luckydrawn;
                LuckyOptions = luckyopts;
                ExclusiveWins = exclusive;
            }
            
            public string Slug { get; set; }
            public string Name { get; set; }
            public double Jackpot { get; set; }
            public double Price { get; set; }

            public int MainDrawn { get; set; }
            public int MainOptions { get; set; }
            public int LuckyDrawn { get; set; }
            public int LuckyOptions { get; set; }

            public bool ExclusiveWins { get; set; }

            public List<LottoWin> Wins { get; set; }


            private string GetDebuggerDisplay()
            {
                return $"{Name}: £{Jackpot} for £{Price}";
            }

            (double, LottoWin) exclusiveWin()
            {
                LottoWin bestWin = null;
                double bestValue = 0;
                foreach(var win in Wins)
                {
                    var odds = win.GetOdds(this);
                    var expected = odds * win.Prize;
                    if(expected > bestValue)
                    {
                        bestValue = expected;
                        bestWin = win;
                    }
                }
                return (bestValue, bestWin);
            }
            double sumWin()
            {
                double sum = 0;
                foreach(var win in Wins)
                {
                    if (win.ExtraNeeded == 0 && win.MainNeeded == 0) continue;
                    var odds = win.GetOdds(this);
                    var expected = odds * win.Prize;
                    sum += expected;
                }
                return sum;
            }

            public (double, LottoWin) GetExpectedWin()
            {
                if (ExclusiveWins)
                    return exclusiveWin();
                return (sumWin(), null);
            }
        }

        [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
        class LottoWin
        {
            public LottoWin(double prize, int main, int extra)
            {
                Prize = prize;
                MainNeeded = main;
                ExtraNeeded = extra;
            }
            public double Prize { get; set; }
            public int MainNeeded { get; set; }
            public int ExtraNeeded { get; set; }

            static double domaths(int neededAmount, int drawnAmount, int totalOptions)
            {
                var totalNeededOpts = Program.GetBinCoeff(drawnAmount, neededAmount);
                var remainOptions = Program.GetBinCoeff(totalOptions - drawnAmount, drawnAmount - neededAmount);
                var totalPermutes = Program.GetBinCoeff(totalOptions, drawnAmount);
                return (totalNeededOpts * remainOptions) / (double)totalPermutes;
            }

            public double GetOdds(Lotto lotto)
            {
                var main = domaths(MainNeeded, lotto.MainDrawn, lotto.MainOptions);
                if(ExtraNeeded > 0)
                {
                    main *= domaths(ExtraNeeded, lotto.LuckyDrawn, lotto.LuckyOptions);
                }
                return main;
            }


            public string GetDebuggerDisplay()
            {
                return $"£{Prize}, with {MainNeeded} + {ExtraNeeded}";
            }
        }

        bool endsWithSlice(string name, string ends, out string previous)
        {
            var ind = name.IndexOf(ends);
            previous = null;
            if (ind == -1) return false;
            if ((ind + ends.Length) >= name.Length)
            {
                previous = name.Substring(0, ind);
                return true;
            }
            return false;
        }

        const string BaseUrl = "https://www.national-lottery.co.uk/games";
        
        List<Lotto> GetLottos()
        {
            return new List<Lotto>()
            {
                { new("euromillions", "EuroMillions", 5, 50, 2, 12) },
                { new("lotto", "National Lotto", 6, 59, 1, 59) },
                { new("euromillions-hotpicks", "EuroMillions Hot Picks", 5, 50, exclusive: true) },
                { new("lotto-hotpicks", "National Lotto Hot Picks", 5, 59, exclusive: true) },
                { new("thunderball", "Thunderball", 5, 39, 1, 14) }
            };
        }
        
        public async Task Execute()
        {
            var web = new HtmlWeb();
            var doc = await web.LoadFromWebAsync(BaseUrl);
            var nav = doc.CreateNavigator();


            /*
             <meta name="euromillions-next-draw-date" content="18-04-2023"/>
            <meta name="euromillions-price" content="2.50"/>
            <meta name="euromillions-roll-count" content="6"/>
            <meta name="euromillions-next-draw-jackpot" content="£76,000,000"/>
            <meta name="euromillions-next-draw-jackpot-short" content="£76M"/>
            <meta name="euromillions-next-draw-day" content="Tuesday"/>
            <meta name="euromillions-days-to-next-draw" content="2"/>
            <meta name="euromillions-hours-to-next-draw" content="8"/>
            <meta name="euromillions-minutes-to-next-draw" content="17"/>
             */


            var lottos = new Dictionary<string, Lotto>();
            foreach(var l in GetLottos())
            {
                lottos[l.Slug] = l;
            }
            var metas = nav.Select("//head//meta[@name]");

            foreach(XPathNavigator child in metas)
            {
                var name = child.GetAttribute("name", child.NamespaceURI);
                var content = child.GetAttribute("content", child.NamespaceURI);

                if(endsWithSlice(name, "-price", out var lottoKey) && lottos.TryGetValue(lottoKey, out var lotto))
                {
                    lotto.Price = double.Parse(content);
                } else if(endsWithSlice(name, "-next-draw-jackpot", out lottoKey) && lottos.TryGetValue(lottoKey, out lotto))
                {
                    lotto.Jackpot = double.Parse(content.Substring(1).Replace(",", ""));
                }
            }

            var worth = new List<Tuple<Lotto, double, LottoWin>>();
            foreach((var slug, var lotto) in lottos)
            {
                var results = await getResults(web, slug, (text) =>
                {
                    if(text.StartsWith("Jackpot"))
                    {
                        return lotto.Jackpot;
                    } else if(text.Contains("Lucky Dip"))
                    {
                        return 2;
                    }
                    return -1;
                });
                lotto.Wins = results;

                (var best, var win) = lotto.GetExpectedWin();
                if(best > lotto.Price)
                {
                    var thisJp = lotto.Jackpot.ToString();
                    if(!Data.SeenJackpots.TryGetValue(lotto.Name, out var jp))
                    {
                        jp = null;
                    }
                    if(jp != thisJp)
                    {
                        worth.Add(new(lotto, best, win));
                        Data.SeenJackpots[lotto.Name] = thisJp;
                    }


                }
            }
            if(worth.Count > 0)
                OnSave(); // save seen jackpot changes


            foreach((var channelId, var lottoNames) in Data.Channels)
            {
                var filtered = worth.Where(x => lottoNames.Count == 0 || lottoNames.Contains(x.Item1.Name) || lottoNames.Contains(x.Item1.Slug)).ToList();
                if (filtered.Count == 0) continue;

                var embed = new EmbedBuilder();
                embed.Title = "Lotto returns";
                foreach ((var lotto, var best, var win) in filtered)
                {
                    string d = $"£{Math.Round(best, 2)}";
                    if (win != null)
                        d += $" with {win.GetDebuggerDisplay()}";
                    embed.AddField(lotto.Name, d);
                }
                var chnl = Program.Client.GetChannel(channelId) as ITextChannel;
                if (chnl != null)
                    await chnl.SendMessageAsync(embed: embed.Build());
            }
        }

        async Task<List<LottoWin>> getResults(HtmlWeb web, string slug, Func<string, double> specialConvert)
        {
            var ls = new List<LottoWin>();
            var doc = await web.LoadFromWebAsync(BaseUrl + "/" + slug);
            var nav = doc.CreateNavigator();
            var tableRows = nav.Select("//table[contains(@class, 'prize_table')]//tr");
            foreach(XPathNavigator tr in tableRows)
            {
                var match = tr.SelectSingleNode("td[contains(@class, 'match')]");
                if (match == null) continue;
                int main = 0;
                int extra = 0;
                bool seenPlus = false;
                var drawn = match.Select(".//span[contains(@class, 'drawn')]");
                foreach(XPathNavigator ball in drawn)
                {
                    var cls = ball.GetAttribute("class", ball.NamespaceURI);
                    if (cls.Contains("plus_sign"))
                    {
                        seenPlus = true;
                    } else if(seenPlus)
                    {
                        extra++;
                    } else
                    {
                        main++;
                    }
                }
                var prize = tr.SelectSingleNode("td[contains(@class, 'prize')]");
                var text = prize.InnerXml;
                double amount = -1;
                if(text.StartsWith('&'))
                { // encoded stuff, 
                    var split = text.Split(';');
                    text = split.Last();
                    if(!double.TryParse(text.Replace(",", "").Replace(" ", ""), out amount))
                    {
                        amount = -2;
                    }

                } else
                {
                    amount = specialConvert(text);
                }
                
                ls.Add(new LottoWin(amount, main, extra));
            }
            return ls;
        }

        public override void OnReady(IServiceProvider services)
        {
            base.OnReady(services);
#if DEBUG
            Execute().Wait();
#endif
        }

        public override void OnDailyTick()
        {
            Execute().Wait();
        }

        public Task<string> RegisterWithOptionAsync(IMessageChannel channel, IUser user, string option)
        {
            List<string> ls;
            if(!Data.Channels.TryGetValue(channel.Id, out ls))
            {
                ls = new List<string>();
            }
            if(!string.IsNullOrWhiteSpace(option))
                ls.Add(option);
            Data.Channels[channel.Id] = ls.Distinct().ToList();
            OnSave();
            return Task.FromResult("Added!");
        }

        public Task<string> UnregisterWithOptionAsync(IMessageChannel channel, IUser user, string option)
        {
            if(option == null)
            {
                Data.Channels.Remove(channel.Id);
                OnSave();
                return Task.FromResult("Unregistered channel.");
            }
            var ls = Data.Channels[channel.Id];
            ls.Remove(option);
            OnSave();
            return Task.FromResult("Unregistered option");
        }

        public Task<string> RegisterAsync(IMessageChannel channel, IUser user) => RegisterWithOptionAsync(channel, user, null);

        public Task<string> UnregisterAsync(IMessageChannel channel, IUser user) => UnregisterWithOptionAsync(channel, user, null);

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async IAsyncEnumerable<AutocompleteResult> GetOptionsAsync(IMessageChannel channel, IUser user)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var l = GetLottos();
            if (channel == null)
            {
                foreach (var lotto in l)
                    yield return new AutocompleteResult(lotto.Name, lotto.Slug);
            } else
            {
                if (!Data.Channels.TryGetValue(channel.Id, out var ls))
                    ls = new();
                foreach(var lotto in l)
                {
                    if (ls.Contains(lotto.Slug) || ls.Contains(lotto.Name))
                        yield return new(lotto.Name, lotto.Slug);
                }
            }

        }
    }

    public class LottoSave
    {
        public Dictionary<ulong, List<string>> Channels { get; set; } = new();

        public Dictionary<string, string> SeenJackpots = new();
    }
}
