using Discord;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Utils
{
    public static class UserUtils
    {
        public static string GetAnyAvatarUrl(this IUser user, ImageFormat format = ImageFormat.Auto, ushort size = 128)
        {
            var s = user.GetAvatarUrl(format, size);
            if (s == null)
                s = user.GetDefaultAvatarUrl();
            return s;
        }
        public static string GetName(this IGuildUser user) 
            => user == null ? null : (user.Nickname ?? user.Username);
    }
}
