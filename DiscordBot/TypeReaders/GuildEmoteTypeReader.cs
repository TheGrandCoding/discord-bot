using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.TypeReaders
{
    public class GuildEmoteTypeReader : BotTypeReader<GuildEmote>
    {
        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            if (context.Guild == null)
                return TypeReaderResult.FromError(CommandError.UnmetPrecondition, "Must be executed within a guild");
            if (Emote.TryParse(input, out var emote))
            {
                var gem = await context.Guild.GetEmoteAsync(emote.Id);
                return TypeReaderResult.FromSuccess(gem);
            }
            if(ulong.TryParse(input, out var id))
            {
                var gem = await context.Guild.GetEmoteAsync(id);
                return TypeReaderResult.FromSuccess(gem);
            }
            if(input.StartsWith(':') == false || (input.StartsWith(':') && input.EndsWith(':')))
            {
                var name = input.StartsWith(':') ? input[1..^1] : input;
                var gem = context.Guild.Emotes.FirstOrDefault(x => x.Name == name);
                if (gem != null)
                    return TypeReaderResult.FromSuccess(gem);
            }
            return TypeReaderResult.FromError(CommandError.ParseFailed, $"Could not parse input as an emote");
        }
    }
}
