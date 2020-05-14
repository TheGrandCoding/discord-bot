using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DiscordBot.TypeReaders
{
    public abstract class BotTypeReader<T> : TypeReader
    {
        public void Register(CommandService cmds)
        {
            cmds.AddTypeReader<T>(this);
        }
    }
}
