using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.TypeReaders
{
    public class UriTypeReader : BotTypeReader
    {
        public override Type Reads => typeof(Uri);
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            if(Uri.TryCreate(input, UriKind.Absolute, out var res))
            {
                return Task.FromResult(TypeReaderResult.FromSuccess(res));
            }
            return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, $"Could not parse input as an absolute Uri."));
        }
    }
}
