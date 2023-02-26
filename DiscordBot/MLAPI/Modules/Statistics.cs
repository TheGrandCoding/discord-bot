using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{
    public class Statistics : APIBase
    {
        public Statistics(APIContext context) : base(context, "stats")
        {
        }

        [Method("GET"), Path("/statistics")]
        public async Task Raw(int id)
        {
            ReplyFile("base.html", 200);
        }
    }
}
