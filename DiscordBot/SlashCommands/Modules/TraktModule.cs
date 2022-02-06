using Discord;
using Discord.SlashCommands;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [CommandGroup("trakt", "Commands for Trakt")]
    public class TraktModule : BotSlashBase
    {
        public TraktService Service { get; set; }
        [SlashCommand("register", "Sends calendar reminders in this channel")]
        public async Task Register()
        {
            if(!(Interaction.Channel is ITextChannel txt))
            {
                await Interaction.RespondAsync($":x: This must be ran in a server text channel.", ephemeral: true);
                return;
            }
            if(!Service.Users.TryGetValue(Interaction.User.Id, out var save))
            {
                await Interaction.RespondAsync($":information_source: You must first authorize Trakt via the following link:\r\n<{Service.OAuthUri}>",
                    ephemeral: true);
                return;
            }
            save.Channel = Interaction.Channel as ITextChannel;
            Service.OnSave();
            await Interaction.RespondAsync($"Success!\r\nThis channel will now receive messages every day for episodes airing that day.");
        }

        [SlashCommand("where", "Sees where reminders are sent")]
        public async Task Where()
        {
            if (!Service.Users.TryGetValue(Interaction.User.Id, out var save))
            {
                await Interaction.RespondAsync($"Reminders are being sent nowhere. You must first authorize Trakt via the following link:\r\n<{Service.OAuthUri}>",
                    ephemeral: true);
                return;
            }
            if(save.Channel == null)
            {
                await Interaction.RespondAsync($"Reminders are being sent nowhere. You must use the `/trakt register` command in the channel you want.", ephemeral: true);
                return;
            }
            await Interaction.RespondAsync($"Reminders are being sent in {save.Channel.Mention}", ephemeral: true);
        }
        [SlashCommand("remove", "Removes yourself from reminders")]
        public async Task Remove()
        {
            if (Service.Users.Remove(Interaction.User.Id))
            {
                Service.OnSave();
                await Interaction.RespondAsync($"You have been removed from our records.", ephemeral: true);
            }
            else
            {
                await Interaction.RespondAsync($"You were not in our records to begin with", ephemeral: true);
            }
        }
    }
}
