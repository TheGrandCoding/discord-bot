using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("monitor")]
    public class MonitorModule : BotModule
    {
        public MonitorService Service { get; set; }
        [Command("vc")]
        public async Task VC(BotUser user)
        {
            if(!Service.Monitors.TryGetValue(user.Id, out var monitor))
            {
                monitor = new Monitor();
                Service.Monitors[user.Id] = monitor;
            }
            if(monitor.VC.RemoveAll(x => x.Id == Context.User.Id) == 0)
            {
                monitor.VC.Add(Context.BotUser.FirstValidUser);
                await ReplyAsync("Added monitor for user.");
            } else
            {
                await ReplyAsync("Removed monitor for user.")
            }
        }
        [Command("status")]
        public async Task Status(BotUser user)
        {
            if (!Service.Monitors.TryGetValue(user.Id, out var monitor))
            {
                monitor = new Monitor();
                Service.Monitors[user.Id] = monitor;
            }
            if (monitor.Status.RemoveAll(x => x.Id == Context.User.Id) == 0)
            {
                monitor.Status.Add(Context.BotUser.FirstValidUser);
                await ReplyAsync("Added monitor for user.");
            }
            else
            {
                await ReplyAsync("Removed monitor for user.")
            }
        }
    }
}
