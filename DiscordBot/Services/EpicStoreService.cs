using Discord;
using Discord.WebSocket;
using DiscordBot.Classes.Epic;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class EpicStoreService : SavedService
    {
        public const string Url = "https://store-site-backend-static.ak.epicgames.com/" +
            "freeGamesPromotions?locale=en-US&country=GB&allowCountries=GB";
        public Dictionary<ulong, ulong> Channels { get; set; } = new Dictionary<ulong, ulong>();
        public Dictionary<string, DateTime> Games { get; set; } = new Dictionary<string, DateTime>();
        public string GetUrl(EpicStoreElement game)
            => $"https://www.epicgames.com/store/en-US/product/{(game.ProductSlug)}/home";
        public override string GenerateSave()
        {
            var sv = new EpicSave();
            sv.channels = Channels;
            sv.games = Games;
            return Program.Serialise(sv);
        }
        public override void OnReady()
        {
            var sv = Program.Deserialise<EpicSave>(ReadSave());
            Channels = sv.channels ?? new Dictionary<ulong, ulong>();
            Games = sv.games ?? new Dictionary<string, DateTime>();
        }

        public async Task Send(EmbedBuilder builder, string content = null)
        {
            var embed = builder.Build();
            var rm = new List<ulong>();
            foreach(var keypair in Channels)
            {
                var guild = Program.Client.GetGuild(keypair.Key);
                var user = Program.Client.GetUser(keypair.Key);
                if(guild == null && user == null)
                {
                    rm.Add(keypair.Key);
                    continue;
                }
                var txt = Program.Client.GetChannel(keypair.Value) as ISocketMessageChannel;
                if(txt == null)
                {
                    rm.Add(keypair.Key);
                    continue;
                }
                await txt.SendMessageAsync(content, embed: embed);
            }
            foreach (var x in rm)
                Channels.Remove(x);
            if (rm.Count > 0)
                OnSave();
        }

        public EpicGamesPromotions GetPromotions()
        {
            var http = Program.Services.GetRequiredService<Classes.BotHttpClient>();
            var response = http.GetAsync(Url).Result;
            var body = response.Content.ReadAsStringAsync().Result;
            if (!response.IsSuccessStatusCode)
                return null;
            return JsonConvert.DeserializeObject<EpicGamesPromotions>(body);
        }

        public override void OnLoaded()
        {
            OnDailyTick();
        }

        public override void OnDailyTick()
        {
            if (DateTime.Now.DayOfWeek == DayOfWeek.Thursday
                || DateTime.Now.DayOfWeek == DayOfWeek.Friday)
            {
                if (Program.DailyValidateFailed())
                    return;
                if (Channels.Count == 0)
                    return;
                var games = GetPromotions();
                if(DateTime.Now.DayOfWeek == DayOfWeek.Thursday)
                    CurrentSale(games);
                if(DateTime.Now.DayOfWeek == DayOfWeek.Friday || DateTime.Now.Hour >= 16)
                    NextSale(games);
            }
        }

        string getPricePrefix(string code)
        {
            return code switch
            {
                "GBP" => "£",
                "USD" => "$",
                _ => code + " "
            };
        }

        string FormatPrice(EpicTotalPrice price, int value)
        {
            var currency = ((double)value) / Math.Pow(10, price.CurrencyInfo.Decimals);
            return getPricePrefix(price.CurrencyCode) + currency.ToString("F");
        }

        public void CurrentSale(EpicGamesPromotions response)
        {
            var games = response.Data.Catalog.SearchStore.Elements;
            var thing = games.FirstOrDefault(x => x.EffectiveDate.DayOfYear == DateTime.Now.DayOfYear);
            if (thing == null)
                return;
            var builder = new EmbedBuilder();
            builder.Timestamp = thing.EffectiveDate;
            builder.Title = $"Free Game Today";
            builder.Url = GetUrl(thing);
            builder.Description = $"**{thing.Title}** will be free from {thing.EffectiveDate:HH:mm:ss} today.\r\n" +
                $"{thing.Description ?? "This game has no description."}";
            var uri = thing.KeyImages.FirstOrDefault(x => x.Type == EpicImageType.OfferImageWide)?.Url;
            uri ??= thing.KeyImages.FirstOrDefault()?.Url;
            builder.ImageUrl = uri?.ToString();
            var totalPrice = thing.Price.TotalPrice;
            builder.AddField($"Original Price", FormatPrice(totalPrice, totalPrice.OriginalPrice));
            Send(builder).Wait();
        }
        public void NextSale(EpicGamesPromotions response)
        {
            var games = response.Data.Catalog.SearchStore.Elements.OrderBy(x => x.EffectiveDate);
            var thing = games.ElementAt(1);
            var builder = new EmbedBuilder();
            builder.Timestamp = thing.EffectiveDate;
            builder.Title = $"Free Game Next Thursday";
            builder.Url = GetUrl(thing);
            builder.Description = $"**{thing.Title}** will be free on Thursday.\r\n" +
                $"{thing.Description ?? "This game has no description."}";
            var uri = thing.KeyImages.FirstOrDefault(x => x.Type == EpicImageType.OfferImageWide)?.Url;
            uri ??= thing.KeyImages.FirstOrDefault()?.Url;
            builder.ImageUrl = uri?.ToString();
            var totalPrice = thing.Price.TotalPrice;
            builder.AddField($"Current Price", FormatPrice(totalPrice, totalPrice.OriginalPrice));
            Send(builder).Wait();
        }

    }
    class EpicSave
    {
        public Dictionary<ulong, ulong> channels { get; set; } = new Dictionary<ulong, ulong>();
        public Dictionary<string, DateTime> games { get; set; } = new Dictionary<string, DateTime>();
    }
}
