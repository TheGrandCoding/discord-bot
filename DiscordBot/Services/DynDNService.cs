using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class DynDNService : SavedService
    {
        public string URL { get; set; }
        public override string GenerateSave()
        {
            return URL;
        }
        public override void OnReady()
        {
            URL = ReadSave("");
        }
        public async Task<HttpResponseMessage> Perform()
        {
            var client = Program.Services.GetRequiredService<Classes.BotHttpClient>();
            return await client.GetAsync(URL);
        }
        public override void OnDailyTick()
        {
            if(!string.IsNullOrWhiteSpace(URL))
            {
                Perform().Wait();
            }
        }
    }
}
