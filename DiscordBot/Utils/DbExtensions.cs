using DiscordBot.Classes.DbContexts;
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
        public static BotDbContext GetBotDb(this IServiceProvider services, string reason)
            => GetDb<BotDbContext>(services, reason);
        public static LogContext GetMsgDb(this IServiceProvider services, string reason)
            => GetDb<LogContext>(services, reason);
        public static FoodDbContext GetFoodDb(this IServiceProvider services, string reason)
            => GetDb<FoodDbContext>(services, reason);
        public static TimeTrackDb GetTimeDb(this IServiceProvider services, string reason)
            => GetDb<TimeTrackDb>(services, reason);
        public static RssDbContext GetRssDb(this IServiceProvider services, string reason)
            => GetDb<RssDbContext>(services, reason);

        public static Classes.BotHttpClient GetHttp(this IServiceProvider services)
            => services.GetRequiredService<Classes.BotHttpClient>();
    }

}
