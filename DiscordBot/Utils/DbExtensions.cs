using DiscordBot.Classes;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace DiscordBot.Utils
{
    public static class DbExtensions
    {
        public static T GetDb<T>(this IServiceProvider services, string reason) where T : AbstractDbBase
        {
            var d = services.GetRequiredService<T>();
            d.SetReason(reason);
            return d;
        }
        public static Classes.BotDbContext GetBotDb(this IServiceProvider services, string reason)
            => GetDb<Classes.BotDbContext>(services, reason);
        public static Services.LogContext GetMsgDb(this IServiceProvider services, string reason)
            => GetDb<Services.LogContext>(services, reason);
        public static Services.FoodDbContext GetFoodDb(this IServiceProvider services, string reason)
            => GetDb<Services.FoodDbContext>(services, reason);
        public static Services.TimeTrackDb GetTimeDb(this IServiceProvider services, string reason)
            => GetDb<Services.TimeTrackDb>(services, reason);
        public static Services.RssDbContext GetRssDb(this IServiceProvider services, string reason)
            => GetDb<Services.RssDbContext>(services, reason);

        public static Classes.BotHttpClient GetHttp(this IServiceProvider services)
            => services.GetRequiredService<Classes.BotHttpClient>();
    }

}
