using Discord.Commands;
using DiscordBot.Classes.Calender;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.TypeReaders
{
    public abstract class EnumTypeReader<TEnum> : BotTypeReader<TEnum> where TEnum : struct
    {
        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            if (Enum.TryParse<TEnum>(input, out var e))
                return TypeReaderResult.FromSuccess(e);
            return TypeReaderResult.FromError(CommandError.ParseFailed, $"Could not parse '{input}' as {typeof(TEnum).Name}");
        }
    }

    public class CalenderVisibilityTypeReader : EnumTypeReader<EventVisibility>
    {
    }
}
