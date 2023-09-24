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

        private string run_cmd(string args)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = Program.Configuration["urls:ocrexe"];
            start.Arguments = string.Format("\"{0}\" \"{1}\"", Program.Configuration["urls:ocrpy"], args);
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
        public async Task Execute()
        {
            if(string.IsNullOrEmpty(Context.Body))
            {
                await RespondError(APIErrorResponse.InvalidFormBody().EndRequired());
                return;
            }
            var split = Context.Body.IndexOf(',');
            var kind = Context.Body.Substring(0, Context.Body.IndexOf(';'));
            if (kind.Contains("png"))
                kind = "png";
            else
                kind = "jpg";
            var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"download.{kind}");
            try
            {
                var bytes = Convert.FromBase64String(Context.Body.Substring(split + 1));
                System.IO.File.WriteAllBytes(temp, bytes);
                var rtn = run_cmd(temp).Trim();
                Program.LogInfo(rtn, "OCR");
                await RespondRaw(rtn);
            } finally
            {
                try
                {
                    File.Delete(temp);
                } catch { }
            }
        }
    }
}
