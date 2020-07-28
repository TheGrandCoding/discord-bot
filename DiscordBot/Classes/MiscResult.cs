using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes
{
    public class MiscResult : IResult
    {
        private MiscResult(string err)
        {
            ErrorReason = err;
            IsSuccess = string.IsNullOrWhiteSpace(err);
        }
        public CommandError? Error => throw new NotImplementedException();

        public string ErrorReason { get; }

        public bool IsSuccess { get; }

        public static MiscResult FromError(string error) => new MiscResult(error);
        public static MiscResult FromSuccess() => new MiscResult(null);
    }
}
