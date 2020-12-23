using Discord;
using Discord.Commands;
using DiscordBot.Commands;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Name("Xkcd")]
    public class XkcdModule : BotModule
    {
        public XkcdService Service { get; set; }
        [Command("xkcd")]
        [Summary("Sets, or removes, xkcd comics to be presented in the current channel")]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task Toggle()
        {
            if(Service.Channels.TryGetValue(Context.Guild.Id, out var chnl))
            {
                if(chnl == Context.Channel.Id)
                {
                    Service.Channels.Remove(Context.Guild.Id);
                    Service.OnSave();
                    Success("Xkcd will no longer be sent to this guild.");
                } else
                {
                    Service.Channels[Context.Guild.Id] = Context.Channel.Id;
                    Service.OnSave();
                    Success($"Xkcd is now sent to <#{Context.Channel.Id}> rather than <#{chnl}>");
                }
            } else
            {
                Service.Channels[Context.Guild.Id] = Context.Channel.Id;
                Service.OnSave();
                Success($"All future xkcd will be sent to <#{Context.Channel.Id}>");
            }
        }
    }
}
