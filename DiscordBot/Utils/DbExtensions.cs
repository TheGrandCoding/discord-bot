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
    }

}
