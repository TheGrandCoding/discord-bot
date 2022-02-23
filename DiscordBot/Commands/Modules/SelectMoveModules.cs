using Discord.Commands;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("select_move")]
    public class SelectMoveModules : BotBase
    {
        public SelectMoveService Service { get; set; }
        [Command("create")]
        public async Task Create([Remainder]string optionArray)
        {
            if(!Service.UserCycles.TryGetValue(Context.User.Id, out var sv))
            {
                sv = new SelectMoveService.Save();
                Service.UserCycles[Context.User.Id] = sv;
                sv.Message = await Context.Channel.SendMessageAsync("[...]");
            }

            string[] options;
            if (optionArray.Contains("\n"))
                options = optionArray.Split("\n");
            else
                options = optionArray.Split(",");




            sv.Cycle = options;
            sv.Index = 0;
            await sv.Update();
            Service.OnSave();
        }
    }
}
