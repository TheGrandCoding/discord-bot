using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    public class Login : APIBase
    {
        public Login(APIContext c) : base(c, "login") { }

        [Method("GET"), Path("/login")]
        public void LoginBase()
        {

        }
    }
}
