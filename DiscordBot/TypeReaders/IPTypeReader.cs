using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.TypeReaders
{
    public class IPTypeReader : BotTypeReader<IPAddress>
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            if (IPAddress.TryParse(input, out var ip))
                return Task.FromResult(TypeReaderResult.FromSuccess(ip));
            return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Could not parse IP address"));
        }
    }
}
