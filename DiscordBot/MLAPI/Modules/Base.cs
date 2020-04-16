using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    public class Base : APIBase
    {
        public Base(APIContext context) : base(context, "")
        {
        }

        [Path("/"), Method("GET")]
        public void Thing()
        {
            ReplyFile("_base.html", 200);
        }
    }
}
