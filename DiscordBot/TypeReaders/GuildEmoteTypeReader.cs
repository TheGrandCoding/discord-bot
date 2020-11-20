using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
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
            if (!Emote.TryParse(input, out var emote))
                return TypeReaderResult.FromError(CommandError.ParseFailed, $"Could not parse input as an emote");
            var gem = await context.Guild.GetEmoteAsync(emote.Id);
            return TypeReaderResult.FromSuccess(gem);
        }
    }
}
