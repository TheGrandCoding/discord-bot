using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.ServerList
{
    public class MCServer : APIBase
    {
        public MCServer(APIContext context) : base(context, "mc")
        {
        }

        public static string Saved = "";
        [Method("GET"), Path("/mc/hamIp")]
        public async Task GetDetails()
        {
            if(string.IsNullOrWhiteSpace(Saved))
            {
                await RespondRaw("", 204);
            } else
            {
                await RespondRaw(Saved, 200);
            }
        }

        [Method("GET"), Path("/mc/sethamIp")]
        public async Task PostDetails(string ip, string port)
        {
            if(!IPAddress.TryParse(ip, out _))
            {
                await RespondRaw("Bad IP", 400);
                return;
            }
            if(!int.TryParse(port, out _))
            {
                await RespondRaw("Bad port", 400);
                return;
            }
            Saved = $"{ip}:{port}";
            await RespondRaw("", 200);
        }
    }
}
