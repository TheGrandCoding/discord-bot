using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBot.TypeReaders
{
    public class EmoteTypeReader : BotTypeReader<IEmote>
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            if(input.StartsWith('<'))
            {
                if (Emote.TryParse(input, out var thing))
                    return Task.FromResult(TypeReaderResult.FromSuccess(thing));
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, $"Unable to interpret input as a non-Unicode emote."));
            } else
            {
                if (Regex.IsMatch(input, @"(\u00a9|\u00ae|[\u2000-\u3300]|\ud83c[\ud000-\udfff]|\ud83d[\ud000-\udfff]|\ud83e[\ud000-\udfff])"))
                {
                    return Task.FromResult(TypeReaderResult.FromSuccess(new Emoji(input)));
                }
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Unable to interpret input as a Unicode emoji."));
            }
        }
    }
}
