using Discord;
using Discord.Interactions;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [Group("trakt", "Commands for Trakt")]
    [RequireContext(ContextType.Guild)]
    public class TraktModule : BotSlashBase
    {
        public TraktService Service { get; set; }
        [SlashCommand("register", "Sends calendar reminders in this channel")]
        public async Task Register()
        {
            if(!(Context.Channel is ITextChannel txt))
            {
                await RespondAsync($":x: This must be ran in a server text channel.", ephemeral: true);
                return;
            }
            if(!Service.Users.TryGetValue(Context.User.Id, out var save))
            {
                await RespondAsync($":information_source: You must first authorize Trakt via the following link:\r\n<{Service.OAuthUri}>",
                    ephemeral: true);
                return;
            }
            save.Channel = Context.Channel as ITextChannel;
            Service.OnSave();
            await RespondAsync($"Success!\r\nThis channel will now receive messages every day for episodes airing that day.");
        }

        [SlashCommand("where", "Sees where reminders are sent")]
        public async Task Where()
        {
            if (!Service.Users.TryGetValue(Context.User.Id, out var save))
            {
                await RespondAsync($"Reminders are being sent nowhere. You must first authorize Trakt via the following link:\r\n<{Service.OAuthUri}>",
                    ephemeral: true);
                return;
            }
            if(save.Channel == null)
            {
                await RespondAsync($"Reminders are being sent nowhere. You must use the `/trakt register` command in the channel you want.", ephemeral: true);
                return;
            }
            await RespondAsync($"Reminders are being sent in {save.Channel.Mention}", ephemeral: true);
        }
        [SlashCommand("remove", "Removes yourself from reminders")]
        public async Task Remove()
        {
            if (Service.Users.Remove(Context.User.Id))
            {
                Service.OnSave();
                await RespondAsync($"You have been removed from our records.", ephemeral: true);
            }
            else
            {
                await RespondAsync($"You were not in our records to begin with", ephemeral: true);
            }
        }
    }
}
