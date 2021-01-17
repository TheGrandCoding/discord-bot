using Discord.Commands;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("epic")]
    [Name("Epic Store")]
    [RequireContext(ContextType.Guild)]
    public class EpicGamesModule : BotBase
    {
        public EpicStoreService Service { get; set; }

        [Command("register")]
        [Summary("Registers the context channel as one to send updates to")]
        public async Task Register()
        {
            Service.Channels[Context.Guild.Id] = Context.Channel.Id;
            Service.OnSave();
            await ReplyAsync($"Bot will now update on free games in store in this channel");
#if DEBUG
            Service.OnDailyTick();
#endif
        }

        [Command("unregister")]
        [Summary("Unregisters the context guild from updatse")]
        public async Task Unregister()
        {
            Service.Channels.Remove(Context.Guild.Id);
            Service.OnSave();
            await ReplyAsync($"Bot will no longer send any updates on free games to this guild");
        }
    }
}
