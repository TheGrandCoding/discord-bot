using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.TypeReaders
{
    public class BotDbUserTypeReader : BotTypeReader<BotDbUser>
    {
        public async override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            using var db = BotDbContext.Get("BotDbTypeReader");
            if(uint.TryParse(input, out var dbId))
            {
                var dbUser = await db.GetUserAsync(dbId);
                if(dbUser != null)
                    return TypeReaderResult.FromSuccess(dbUser);
            }
            if(ulong.TryParse(input, out var id) || MentionUtils.TryParseUser(input, out id))
            {
                var res = await db.GetUserFromDiscord(id, true);
                if(res.Success)
                    return TypeReaderResult.FromSuccess(res.Value);
            }
            var u2 = db.Users.FirstOrDefault(x => x.Name == input || x.Name == input);
            if (u2 != null)
                return TypeReaderResult.FromSuccess(u2);
            //var result = await new UserTypeReader<IUser>().ReadAsync(context, input, services);
            //return result;
            return TypeReaderResult.FromError(CommandError.ParseFailed, "Failed to get user from that input");
        }
    }
}
