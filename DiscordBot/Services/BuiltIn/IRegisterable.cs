using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services.BuiltIn
{
    public interface IRegisterable
    {
        Task<string> RegisterAsync(IMessageChannel channel, IUser user);
        Task<string> UnregisterAsync(IMessageChannel channel, IUser user);
    }

    public interface IRegisterableOption : IRegisterable
    {
        Task<string> RegisterWithOptionAsync(IMessageChannel channel, IUser user, string option);
        Task<string?> UnregisterWithOptionAsync(IMessageChannel channel, IUser user, string option);

        IAsyncEnumerable<AutocompleteResult> GetOptionsAsync(IMessageChannel channel, IUser user);
    }
}
