using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace DiscordBot.MLAPI.Modules.ServerList
{
    public class MCServer : APIBase
    {
        public MCServer(APIContext context) : base(context, "mc")
        {
        }

        public static string Saved = "";
        [Method("GET"), Path("/mc/hamIp")]
        public void GetDetails()
        {
            if(string.IsNullOrWhiteSpace(Saved))
            {
                RespondRaw("", 204);
            } else
            {
                RespondRaw(Saved, 200);
            }
        }

        [Method("GET"), Path("/mc/sethamIp")]
        public void PostDetails(string ip, string port)
        {
            if(!IPAddress.TryParse(ip, out _))
            {
                RespondRaw("Bad IP", 400);
                return;
            }
            if(!int.TryParse(port, out _))
            {
                RespondRaw("Bad port", 400);
                return;
            }
            Saved = $"{ip}:{port}";
            RespondRaw("", 200);
        }
    }
}
