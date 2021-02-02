using Discord;
using Discord.Webhook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services.BuiltIn
{
    public class WebhookService : Service
    {
        const string _name = "mlapi-webhook";
        Semaphore _lock = new Semaphore(1, 1);
        Dictionary<ulong, IWebhook> cache = new Dictionary<ulong, IWebhook>();
        public async Task<DiscordWebhookClient> GetWebhookClientAsync(ITextChannel channel)
        {
            _lock.WaitOne();
            try
            {
                if (cache.TryGetValue(channel.Id, out var correct))
                    return new DiscordWebhookClient(correct);
                var hooks = await channel.GetWebhooksAsync();
                correct = hooks.FirstOrDefault(x => x.Name == _name);
                if (correct == null)
                {
                    correct = await channel.CreateWebhookAsync(_name);
                }
                cache[channel.Id] = correct;
                return new DiscordWebhookClient(correct);
            }  
            finally
            {
                _lock.Release();
            }
        }
    }
}
