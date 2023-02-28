using Discord;
using Discord.Interactions;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Modules
{
    [Group("experiments", "Register or search guilde experiments")]
    [DefaultMemberPermissions(GuildPermission.ManageGuild)]
    [DefaultPermission(false)]
    public class Experiments : BotSlashBase
    {
        public DsRolloutService Service { get; set; }

        [SlashCommand("refresh", "Updates all experiment embeds across all guilds")]
        public async Task Refresh()
        {
            if(Service == null || Service.State == ServiceState.Ready)
            await RespondAsync("Starting refresh...", ephemeral: true);
            var sw = Stopwatch.StartNew();
            foreach (var exp in Service.Experiments.Values)
            {
                await Service.sendMessageFor(exp, null);
            }
            sw.Stop();
            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = $"First step done in {sw.Elapsed}";
            });
            sw.Start();
            await Service.updateTask();
            sw.Stop();
            await ModifyOriginalResponseAsync(x => { x.Content = $"Refresh completed after {sw.Elapsed}"; });
        }

        [SlashCommand("check", "Checks which treatment the server is in")]
        public async Task HasExperiment([Autocomplete(typeof(ExperimentsAutocomplete))]string experiment, string serverid = null)
        {
            if (string.IsNullOrWhiteSpace(serverid))
                serverid = Context.Guild.Id.ToString();
            if(!ulong.TryParse(serverid, out var id))
            {
                await RespondAsync("Server ID must be a ulong", ephemeral: true);
                return;
            }
            if(!Service.Experiments.TryGetValue(experiment, out var exp))
            {
                await RespondAsync("Unknown experiment", ephemeral: true);
                return;
            }
            var guild = Program.Client.GetGuild(id);
            string treatment;
            StringBuilder log = new StringBuilder();
            if(guild == null)
            {
                treatment = exp.GetTreatment(id, null, null, log);
            } else
            {
                treatment = exp.GetTreatment(guild, log);
            }

            if(treatment == null)
            {
                await RespondAsync($"Server is ineligible for this experiment due to filters.\r\n\r\n```\r\n{log}\r\n```");
            } else
            {
                await RespondAsync($"Server is in {treatment}\r\n\r\n```\r\n{log}\r\n```");
            }

        }
    }

    public class ExperimentsAutocomplete : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            var srv = Program.GlobalServices.GetRequiredService<DsRolloutService>();
            var results = await srv.GetAutocomplete(autocompleteInteraction);
            return AutocompletionResult.FromSuccess(results.Take(20));
        }
    }
}
