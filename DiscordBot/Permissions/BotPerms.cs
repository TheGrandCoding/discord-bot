using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot
{
    public static partial class Perms
    {
        public static class Bot
        {
            [Description("All bot permissions")]
            public const string All = "bot.*";

            [AssignedBy(Bot.All)]
            public static class Developer
            {
                [Description("All bot-development permissions")]
                public const string All = "bot.developer.*";
                [Description("View todays bot log")]
                public const string SeeLatestLog = "bot.developer.latest";
                [Description("View bot logs, even from other users")]
                public const string SeeAPILogs = "bot.developer.apilogs";
                [Description("Link Discord and Chess accounts ")]
                public const string ConnectChess = "bot.developer.connect";
                [Description("Make technical modifications to a Chess players true rating")]
                public const string SetActualChessRating = "bot.developer.truerating";
                [Description("View other users permissions")]
                public const string ViewPermissions = "bot.developer.seeperms";
                [Description("View nicknames of users via MLAPI, even if not present in said server")]
                public const string ViewNicknamesAllServers = "bot.developer.nicknames";
            }

            [AssignedBy(Bot.All)]
            public static class User
            {
                [Description("All user permissions")]
                public const string All = "bot.user.*";

                [Description("Change own override name")]
                public const string ChangeSelfName = "bot.user.changename_self";

                [Description("View other user's override name")]
                public const string ViewOtherName = "bot.user.viewname_other";

                [Description("Change other user's override name")]
                public const string ChangeOtherName = "bot.user.changename_other";
            }

            [Description("Approve a new user to use API")]
            public const string ApproveUser = "bot.approve";
        }
    }
}
