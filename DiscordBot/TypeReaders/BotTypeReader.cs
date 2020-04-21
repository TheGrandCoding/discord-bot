using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DiscordBot.TypeReaders
{
    public abstract class BotTypeReader : TypeReader, IComparable<BotTypeReader>
    {
        public abstract Type Reads { get; }

        public int CompareTo([AllowNull] BotTypeReader other)
        {
            return Reads.Name.CompareTo(other?.Reads?.Name);
        }
    }
}
