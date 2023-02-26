using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{
    public class GroupGame : AuthedAPIBase
    {
        public GroupGame(APIContext context) : base(context, "")
        {
        }

        [Method("GET"), Path("/game")]
        public async Task Base()
        {
            ReplyFile("groupgame.html", 200);
        }
    }
}
