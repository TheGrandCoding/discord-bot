using Discord.Commands;
using DiscordBot.Commands;
using DiscordBot.Services;
using DiscordBot.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Classes
{
    [Group("fun")]
    [RequireContext(ContextType.Guild)]
    [Name("Fun Commands")]
    [RequireUserPermission(Discord.GuildPermission.ManageMessages)]
    public class FunModule : BotBase
    {
        public FunService Service { get; set; }

        [Command("list")]
        [Summary("Lists all triggers")]
        public async Task List()
        {
            await ReplyAsync(string.Join("\r\n", Service.ImageTriggers.Keys));
        }

        [Command("add")]
        [Summary("Add image trigger")]
        public async Task Add(string trigger, Uri image)
        {
            Service.ImageTriggers.AddInner(trigger, image.ToString());
            await Success("Added.");
        }

        [Command("remove")]
        [Summary("Remove trigger")]
        [RequireOwner]
        public async Task Remove(string trigger)
        {
            if(Service.ImageTriggers.Remove(trigger, out var ls))
            {
                await ReplyAsync($"Removed {trigger} with following keys:\r\n {string.Join("\r\n", ls)}");
            } else
            {
                await ReplyAsync("That trigger does not exist");
            }
        }
    }
}
