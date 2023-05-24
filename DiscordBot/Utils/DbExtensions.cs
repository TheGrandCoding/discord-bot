using Microsoft.Extensions.DependencyInjection;
using System;

namespace DiscordBot.Utils
{
    public static class DbExtensions
    {
        public static Classes.BotDbContext GetBotDb(this IServiceProvider services, string reason)
        {
            var d = services.GetRequiredService<Classes.BotDbContext>();
            d.SetReason(reason);
            return d;
        }
        public static Services.LogContext GetMsgDb(this IServiceProvider services, string reason)
        {
            var d = services.GetRequiredService<Services.LogContext>();
            d.SetReason(reason);
            return d;
        }
        public static Services.FoodDbContext GetFoodDb(this IServiceProvider services, string reason)
        {
            var d = services.GetRequiredService<Services.FoodDbContext>();
            return d;
        }
        public static Services.TimeTrackDb GetTimeDb(this IServiceProvider services, string reason)
        {
            var d = services.GetRequiredService<Services.TimeTrackDb>();
            d.SetReason(reason);
            return d;
        }
        public static Services.RssDbContext GetRssDb(this IServiceProvider services)
        {
            var d = services.GetRequiredService<Services.RssDbContext>();
            return d;
        }

        public static Classes.BotHttpClient GetHttp(this IServiceProvider services)
            => services.GetRequiredService<Classes.BotHttpClient>();
    }

}
