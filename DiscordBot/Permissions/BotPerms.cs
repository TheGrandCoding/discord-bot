using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace DiscordBot
{
    public static partial class Perms
    {
        public static class Bot
        {
            [Description("All bot permissions")]
            public const string All = "bot.*";

            public static class Developer
            {
                [Description("All bot-development permissions")]
                public const string All = "bot.developer.*";
                [Description("View todays bot log")]
                public const string SeeLatestLog = "bot.developer.latest";
                [Description("Link Discord and Chess accounts ")]
                public const string ConnectChess = "bot.developer.connect";
                [Description("Make technical modifications to a Chess players true rating")]
                public const string SetActualChessRating = "bot.developer.truerating";
                [Description("View other users permissions")]
                public const string ViewPermissions = "bot.developer.seeperms";
            }
        }
    }
}
