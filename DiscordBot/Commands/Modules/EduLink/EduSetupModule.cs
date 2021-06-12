#if INCLUDE_EDULINK
using Discord.Commands;
using DiscordBot.Commands;
using DiscordBot.Commands.Attributes;
using DiscordBot.Services;
using EduLinkDLL;
using EduLinkDLL.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules.EduLink
{
    [Group("edulink")]
    [Name("EduLink One")]
    public class EduSetupModule : BotBase
    {
        public EduLinkService Service { get; set; }
        [Command("setup")]
        [Summary("Sets up your EduLink account information.")]
        public async Task<RuntimeResult> Setup(string username, [Sensitive][Remainder]string password)
        {
            var hadPrior = Service.Clients.Remove(Context.User.Id);

            var client = new EduLinkClient();
            client.Log = EduLinkService.logHandler;
            try
            {
                var successLogin = await client.LoginAsync(username, password, 60);
                if (!successLogin)
                    return new BotResult("Failed to login for an unknown reason");
            } catch (EduLinkException ex)
            {
                return new BotResult($"Failed to login: {ex.Message}");
            }
            Service.Clients[Context.User.Id] = client;
            Service.OnSave();
            await ReplyAsync($"Account information {(hadPrior ? "updated" : "registered")} to {client.CurrentUser.Forename}\r\n" +
                $"Please delete your message.");
            return new BotResult();
        }

        [Command("info")]
        [Summary("Gets information on the account registered, if one is.")]
        public async Task Information()
        {
            if(!Service.Clients.TryGetValue(Context.User.Id, out var client))
            {
                await ReplyAsync($"No account is linked, use `{Program.Prefix}edulink setup [username] [password]` to do so");
                return;
            }
            await ReplyAsync($"Account is linked to {client.CurrentUser.UserName}, {client.CurrentUser.Forename}");
        }

        [Command("roles")]
        [RequirePermission(Perms.Bot.Developer.All)]
        public async Task SetRoles()
        {
            var service = Program.Services.GetRequiredService<ClassRoleService>();
            await service.perform();
            await ReplyAsync("Done.");
        }
    }
}
#endif