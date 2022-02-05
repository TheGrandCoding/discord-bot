using Discord;
using Discord.SlashCommands;
using Discord.WebSocket;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [CommandGroup("bot", "Bot developer commands")]
    [DefaultDisabled]
    public class BotDevCmds : BotSlashBase
    {
        async Task<bool> Forbidden()
        {
            if(Interaction.User.Id != Program.AppInfo.Owner.Id)
            {
                await Interaction.RespondAsync(":x: You are forbidden from using this command.", ephemeral: true);
                return true;
            }
            return false;
        }
        public static IEnumerable<AutocompleteResult> GetAutocompleteResults(SocketAutocompleteInteraction interaction)
        {
            var list = new List<AutocompleteResult>();
            string text = interaction.Data.Current.Value as string;

            foreach(var srv in Service.GetServices())
            {
                if (string.IsNullOrWhiteSpace(text) || srv.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
                    list.Add(new AutocompleteResult(srv.Name, srv.Name));
            }
            return list.Take(20);
        }

        [SlashCommand("dtick", "Daily tick all, or the specified, service(s)")]
        public async Task DailyTick([Autocomplete]string service = null)
        {
            if (await Forbidden()) return;
            if(string.IsNullOrWhiteSpace(service))
            {
                await Interaction.RespondAsync("Sending daily tick...");
                var sw = Stopwatch.StartNew();
                Service.SendDailyTick();
                sw.Stop();
                await Interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Content = $"Finished daily tick after {sw.ElapsedMilliseconds}ms";
                });
            } else
            {
                var all = Service.GetServices();
                var wanted = all.FirstOrDefault(x => x.Name == service);
                if(wanted == null)
                {
                    await Interaction.RespondAsync("No service by that name recognised");
                } else {
                    await Interaction.RespondAsync("Sending daily tick...");
                    var sw = Stopwatch.StartNew();
                    wanted.OnDailyTick();
                    sw.Stop();
                    await Interaction.ModifyOriginalResponseAsync(x =>
                    {
                        x.Content = $"Finished daily tick after {sw.ElapsedMilliseconds}ms";
                    });
                }
            }
        }
    
        [SlashCommand("getsave", "Gets a service save file")]
        public async Task DownloadServiceFile([Required][Autocomplete]string service)
        {
            if (await Forbidden()) return;
            if(string.IsNullOrWhiteSpace(service))
            {
                await Interaction.RespondAsync(":x: You must specify a service name", ephemeral: true);
                return;
            }
            var srv = Service.GetServices().FirstOrDefault(x => x.Name == service);
            if(srv == null)
            {
                await Interaction.RespondAsync(":x: That service does not exist", ephemeral: true);
                return;
            }
            if(!(srv is SavedService sv))
            {
                await Interaction.RespondAsync(":x: That service does not have a save file", ephemeral: true);
                return;
            }
            await Interaction.RespondAsync($"Fetching save file `{sv.SaveFile}`...", ephemeral: true);
            await Interaction.FollowupWithFileAsync(sv.SaveFile, $"Save file for service {sv.Name}", ephemeral: true);
        }
    }
}
