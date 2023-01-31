using DiscordBot.Classes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{
    public class OCR : APIBase
    {
        public OCR(APIContext ctx) : base(ctx, "ocr") 
        { 
        
        }

#if DEBUG
        const string pypath = @"D:\_GitHub\mlapibot\run.py";
#else
        const string pypath = @"~/bot/mlapibot/run.py";
#endif
        private string run_cmd(string cmd, string args)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = "python3";
            start.Arguments = string.Format("\"{0}\" \"{1}\"", cmd, args);
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            using (Process process = Process.Start(start))
            {
                using (StreamReader reader = process.StandardOutput)
                {
                    string result = reader.ReadToEnd();
                    return result;
                }
            }
        }

        [Method("POST"), Path("/ocr")]
        public void Execute()
        {
            if(string.IsNullOrEmpty(Context.Body))
            {
                RespondError(APIErrorResponse.InvalidFormBody().EndRequired());
                return;
            }
            var split = Context.Body.IndexOf(',');
            var kind = Context.Body.Substring(0, Context.Body.IndexOf(';'));
            if (kind.Contains("png"))
                kind = "png";
            else
                kind = "jpg";
            var bytes = Convert.FromBase64String(Context.Body.Substring(split + 1));
            var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"download.{kind}");
            System.IO.File.WriteAllBytes(temp, bytes);
            var rtn = run_cmd(pypath, temp).Trim();
            Program.LogInfo(rtn, "OCR");
            RespondRaw(rtn);
        }
    }
}
