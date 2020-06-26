using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DiscordBot.MLAPI.Modules.Bot
{
    
    public class Internal : APIBase
    {
        public Internal(APIContext c) : base(c, "/") { }

        [Method("GET"), Path("/bot/restart")]
        public void CloseBot()
        {
            Program.LogMsg("Starting to restart due to request", Discord.LogSeverity.Critical, "Internal");
            Program.Save(true);
            RespondRaw("OK", 200);
#if LINUX
            ProcessStartInfo Info = new ProcessStartInfo();
            Info.Arguments = "-c ping 127.0.0.1 -c 2 && /home/pi/Desktop/runasbot.sh new";
            //Info.WindowStyle = ProcessWindowStyle.Normal;
            //Info.CreateNoWindow = true;
            Info.FileName = "/bin/bash";
            Process.Start(Info);
#endif
            Environment.Exit(0);
        }
    }
}
