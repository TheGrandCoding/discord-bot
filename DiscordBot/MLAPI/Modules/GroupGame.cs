using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    public class GroupGame : AuthedAPIBase
    {
        public GroupGame(APIContext context) : base(context, "")
        {
        }

        [Method("GET"), Path("/game")]
        public void Base()
        {
            ReplyFile("groupgame.html", 200);
        }
    }
}
