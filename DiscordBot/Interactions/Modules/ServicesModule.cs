using Discord;
using Discord.Interactions;
using DiscordBot.Services;
using DiscordBot.Services.BuiltIn;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Modules
{
    [Group("service", "Commands for channel-service management")]
    [DefaultMemberPermissions(Discord.GuildPermission.ManageChannels)]
    [EnabledInDm(false)]
    [DefaultPermission(false)]
    public class ServicesModule : BotSlashBase
    {

        [SlashCommand("register", "Registers a channel for a service")]
        public async Task Register([Autocomplete(typeof(ListServiceAutocomplete))]string service, ITextChannel channel = null)
        {
            channel ??= Context.Channel as ITextChannel;
            if(string.IsNullOrWhiteSpace(service))
            {
                await RespondAsync(":x: Service was not provided",
                    ephemeral: true);
                return;
            }
            var serv = Service.GetServices().FirstOrDefault(s => s.Name == service);
            if(serv == null)
            {
                await RespondAsync(":x: No service exists by that name",
                    ephemeral: true);
                return;
            }
            if(serv is not IRegisterable registerable)
            {
                await RespondAsync(":x: That service cannot be registered",
                    ephemeral: true);
                return;
            }
            await DeferAsync(ephemeral: true);
            var r = await registerable.RegisterAsync(channel, Context.User);
            await ModifyOriginalResponseAsync(x => x.Content = (r ?? "Completed."));
        }
        [SlashCommand("unregister", "Removes a channel from a service")]
        public async Task UnRegister([Autocomplete(typeof(ListServiceAutocomplete))] string service, ITextChannel channel = null)
        {
            channel ??= Context.Channel as ITextChannel;
            if (string.IsNullOrWhiteSpace(service))
            {
                await RespondAsync(":x: Service was not provided",
                    ephemeral: true);
                return;
            }
            var serv = Service.GetServices().FirstOrDefault(s => s.Name == service);
            if (serv == null)
            {
                await RespondAsync(":x: No service exists by that name",
                    ephemeral: true);
                return;
            }
            if (!(serv is IRegisterable registerable))
            {
                await RespondAsync(":x: That service cannot be registered",
                    ephemeral: true);
                return;
            }
            await DeferAsync(ephemeral: true);
            var r = await registerable.UnregisterAsync(channel, Context.User);
            await ModifyOriginalResponseAsync(x => x.Content = (r ?? "Completed."));
        }
    }
}
