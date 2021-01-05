using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Name("Translations")]
    [Group("translate")]
    public class TranslationsModule : BotBase
    {
        public TranslationService Service { get; set; }
        [Command("watch")]
        public async Task Watch(BotUser user)
        {
            if(Service.Watched.TryGetValue(user.Id, out var ls))
            {
                if(ls.RemoveAll(x => x == Context.User.Id) > 0)
                {
                    await ReplyAsync($"Removed watching that user.");
                } else
                {
                    ls.Add(Context.User.Id);
                    await ReplyAsync($"Now watching that user's messages.\r\nYou will be sent a DM translating any non-English messages they send.");
                }
            } else
            {
                Service.Watched[user.Id] = new List<ulong>() { Context.User.Id };
                await ReplyAsync($"Now watching that user's messages.\r\nYou will be sent a DM translating any non-English messages they send.");
            }
            Service.OnSave();
        }
    }
}
