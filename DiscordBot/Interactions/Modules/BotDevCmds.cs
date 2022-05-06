using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Modules
{
    [Group("bot", "Bot developer commands")]
    [DontAutoRegister]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    public class BotDevCmds : BotSlashBase
    {
        async Task<bool> Forbidden()
        {
            if(Context.Interaction.User.Id != Program.AppInfo.Owner.Id)
            {
                await RespondAsync(":x: You are forbidden from using this command.", ephemeral: true);
                return true;
            }
            return false;
        }

        [SlashCommand("dtick", "Daily tick all, or the specified, service(s)")]
        public async Task DailyTick([Autocomplete(typeof(ListServiceAutocomplete))] string service = null)
        {
            if (await Forbidden()) return;
            if(string.IsNullOrWhiteSpace(service))
            {
                await RespondAsync("Sending daily tick...");
                var sw = Stopwatch.StartNew();
                Service.SendDailyTick();
                sw.Stop();
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Content = $"Finished daily tick after {sw.ElapsedMilliseconds}ms";
                });
            } else
            {
                var all = Service.GetServices();
                var wanted = all.FirstOrDefault(x => x.Name == service);
                if(wanted == null)
                {
                    await RespondAsync("No service by that name recognised");
                } else {
                    await RespondAsync("Sending daily tick...");
                    var sw = Stopwatch.StartNew();
                    wanted.OnDailyTick();
                    sw.Stop();
                    await ModifyOriginalResponseAsync(x =>
                    {
                        x.Content = $"Finished daily tick after {sw.ElapsedMilliseconds}ms";
                    });
                }
            }
        }
    
        [SlashCommand("getsave", "Gets a service save file")]
        public async Task DownloadServiceFile([Autocomplete(typeof(ListServiceAutocomplete))]string service)
        {
            if (await Forbidden()) return;
            if(string.IsNullOrWhiteSpace(service))
            {
                await RespondAsync(":x: You must specify a service name", ephemeral: true);
                return;
            }
            var srv = Service.GetServices().FirstOrDefault(x => x.Name == service);
            if(srv == null)
            {
                await RespondAsync(":x: That service does not exist", ephemeral: true);
                return;
            }
            if(!(srv is SavedService sv))
            {
                await RespondAsync(":x: That service does not have a save file", ephemeral: true);
                return;
            }
            await RespondAsync($"Fetching save file `{sv.SaveFile}`...", ephemeral: true);
            string path = Path.Combine(SavedService.SaveFolder, sv.SaveFile);
            await FollowupWithFileAsync(path, $"Save file for service {sv.Name}", ephemeral: true);
        }
    }

    public class ListServiceAutocomplete : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            var list = new List<AutocompleteResult>();
            string text = autocompleteInteraction.Data.Current.Value as string;

            foreach (var srv in Service.GetServices())
            {
                if (string.IsNullOrWhiteSpace(text) || srv.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
                    list.Add(new AutocompleteResult(srv.Name, srv.Name));
            }
            return AutocompletionResult.FromSuccess(list.Take(20));
        }
    }
}
