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
    public class BotUserTypeReader : BotTypeReader<BotUser>
    {
        public async override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            if(ulong.TryParse(input, out var id) || MentionUtils.TryParseUser(input, out id))
            {
                var user = Program.GetUserOrDefault(id);
                if(user == null)
                {
                    var usr = Program.Client.GetUser(id);
                    if (usr == null)
                        return TypeReaderResult.FromError(CommandError.ParseFailed, $"Unknown user by id {id}");
                    user = Program.CreateUser(usr);
                }
                return TypeReaderResult.FromSuccess(user);
            }
            var u2 = Program.Users.FirstOrDefault(x => x.Name == input || x.Username == input);
            if (u2 != null)
                return TypeReaderResult.FromSuccess(u2);
            var result = await new UserTypeReader<IUser>().ReadAsync(context, input, services);
            return result;
        }
    }
}
