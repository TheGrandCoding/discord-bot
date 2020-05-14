using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.TypeReaders
{
    public class GuidTypeReader : BotTypeReader<Guid>
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            if(Guid.TryParse(input, out var guid))
                return Task.FromResult(TypeReaderResult.FromSuccess(guid));
            return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, $"Could not parse input as a GUID."));
        }
    }
}
