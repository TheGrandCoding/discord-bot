using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class ClassRoleService : Service
    {
        public override void OnLoaded()
        {
            OnDailyTick();
        }
        public override void OnDailyTick()
        {
            perform().Wait();
        }

        async Task perform()
        {
            var guild = Program.Client.GetGuild(365230804734967840);
            foreach (var bUser in Program.Users.Where(x => !x.GeneratedUser && !x.ServiceUser && x.Classes.Count > 0).ToList())
            {
                var usr = guild.GetUser(bUser.Id);
                if (usr == null)
                    continue;
                foreach(var keypair in bUser.Classes)
                {
                    if (keypair.Value == "Tutor" || keypair.Value == "StSkills")
                        continue;
                    await perform(guild, usr, keypair.Key);
                    await perform(guild, usr, keypair.Value);
                }
            }
        }

        async Task perform(SocketGuild guild, SocketGuildUser user, string roleName)
        {
            if (user.Roles.Any(x => x.Name == roleName))
                return;
            var role = (IRole)guild.Roles.FirstOrDefault(x => x.Name == roleName);
            role ??= await guild.CreateRoleAsync(roleName, isMentionable:true);
            await user.AddRoleAsync(role);
        }
    }
}
