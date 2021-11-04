using Discord;
using Discord.SlashCommands;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [CommandGroup("experiments", "Register or search guilde experiments")]
    [DefaultDisabled]
    public class Experiments : BotSlashBase
    {
        public DsRolloutService Service { get; set; }

        [SlashCommand("register", "Registers channel to notify changes")]
        public async Task Register()
        {
            Service.Guilds[Interaction.Guild.Id] = new DsRolloutService.GuildSave()
            {
                Channel = Interaction.Channel as ITextChannel,
                Messages = new Dictionary<string, IUserMessage>()
            };

            await Interaction.DeferAsync(true);
            foreach(var exp in Service.Experiments.Values)
            {
                await Service.sendMessageFor(exp, null);
            }

            Service.OnSave();
            await Interaction.FollowupAsync("Done", ephemeral: true);
        }

        [SlashCommand("refresh", "Updates all experiment embeds across all guilds")]
        public async Task Refresh()
        {
            await Interaction.RespondAsync("Starting refresh...", ephemeral: true);
            var sw = Stopwatch.StartNew();
            foreach (var exp in Service.Experiments.Values)
                await Service.sendMessageFor(exp, null);
            sw.Stop();
            await Interaction.FollowupAsync($"Refresh completed after {sw.Elapsed}");
        }

        [SlashCommand("check", "Checks which treatment the server is in")]
        public async Task HasExperiment([Required][Autocomplete]string experiment, string serverid = null)
        {
            if (string.IsNullOrWhiteSpace(serverid))
                serverid = Interaction.Guild.Id.ToString();
            if(!ulong.TryParse(serverid, out var id))
            {
                await Interaction.RespondAsync("Server ID must be a ulong", ephemeral: true);
                return;
            }
            if(!Service.Experiments.TryGetValue(experiment, out var exp))
            {
                await Interaction.RespondAsync("Unknown experiment", ephemeral: true);
                return;
            }
            var guild = Program.Client.GetGuild(id);
            if(guild == null)
            {
                await Interaction.RespondAsync("Unknown server", ephemeral: true);
                return;
            }

            var treatment = exp.GetTreatment(guild);
            if(treatment == null)
            {
                await Interaction.RespondAsync("Server is ineligible for this experiment due to filters.");
            } else
            {
                await Interaction.RespondAsync("Server is in " + treatment);
            }

        }


    }
}
