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
        public async Task Register(
            [Summary("service", "The service to register")]
            [Autocomplete(typeof(ListServiceAutocomplete))]string service, 
            [Summary("option", "An optional key to configure")]
            [Autocomplete(typeof(ListServiceOptionsAutocomplete))]
            string optionValue = null, 
            [Summary("channel", "Use this channel instead")]
            ITextChannel channel = null)
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
            string r;
            if (serv is IRegisterableOption withOpt)
                r = await withOpt.RegisterWithOptionAsync(channel, Context.User, optionValue);
            else
                r = await registerable.RegisterAsync(channel, Context.User);
            await ModifyOriginalResponseAsync(x => x.Content = (r ?? "Completed."));
        }
        [SlashCommand("unregister", "Removes a channel from a service")]
        public async Task UnRegister(
            [Summary("service", "The service to register")]
            [Autocomplete(typeof(ListServiceAutocomplete))]string service,
            [Summary("option", "An optional key to configure")]
            [Autocomplete(typeof(ListServiceOptionsAutocomplete))]
            string optionValue = null,
            [Summary("channel", "Use this channel instead")]
            ITextChannel channel = null)
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
            string r;
            if (serv is IRegisterableOption withOpt)
                r = await withOpt.UnregisterWithOptionAsync(channel, Context.User, optionValue);
            else
                r = await registerable.UnregisterAsync(channel, Context.User);
            await ModifyOriginalResponseAsync(x => x.Content = (r ?? "Completed."));
        }
    }
}
