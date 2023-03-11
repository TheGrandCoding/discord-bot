using DiscordBot.Services.Masterlist;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.ServerList
{
    public class MasterlistModule : APIBase
    {
        public MasterlistModule(APIContext context) : base(context, "masterlist")
        {
            Service = Context.Services.GetRequiredService<MasterlistService>();
        }
        public MasterlistService Service { get; set; }

        [Method("GET"), Path("/masterlist")]
        public async Task ViewMasterlist()
        {
            await ReplyFile("base.html", 200);
        }
    }
}
