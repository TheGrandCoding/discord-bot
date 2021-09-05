using DiscordBot.Services.Masterlist;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Modules.ServerList
{
    public class MasterlistModule : APIBase
    {
        public MasterlistModule(APIContext context) : base(context, "masterlist")
        {
            Service = Program.Services.GetRequiredService<MasterlistService>();
        }
        public MasterlistService Service { get; set; }

        [Method("GET"), Path("/masterlist")]
        public void Base()
        {
            ReplyFile("base.html", 200);
        }
    }
}
