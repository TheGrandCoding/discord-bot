using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    public class Food : AuthedAPIBase
    {
        public Food(APIContext c) : base(c, "food")
        {
        }

        [Method("GET"), Path("/food")]
        public void Base()
        {
            ReplyFile("base.html", 200);
        }

        [Method("GET"), Path("/food/scan")]
        public void Scan()
        {
            ReplyFile("scan.html", 200);
        }
    }
}
