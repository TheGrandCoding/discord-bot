using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace DiscordBot.Services.BuiltIn
{
    public class BackupService : Service
    {
        public override bool IsCritical => true;


        static bool doneOnce = false;
        public override void OnReady()
        {
            if (doneOnce)
                return;
            var latestFolder = Path.Combine(Program.BASE_PATH, "Backups", "Saves", "Latest");
            var oldFolder = Path.Combine(Program.BASE_PATH, "Backups", "Saves", "Old");
            if (!Directory.Exists(latestFolder))
                Directory.CreateDirectory(latestFolder);
            if (!Directory.Exists(oldFolder))
                Directory.CreateDirectory(oldFolder);

            // Step 1: Zip and move any Latest files into Old
            var zipTemp = Path.Combine(oldFolder, "temp.zip");
            ZipFile.CreateFromDirectory(latestFolder, zipTemp);
            File.Move(zipTemp, Path.Combine(oldFolder, $"{DateTime.Now.ToString("yyyy-MM-dd")}.zip"), true);

            // Step 2: Copy current saves into Latest folder
            var mainSave = Path.Combine(Program.BASE_PATH, Program.saveName);
            File.Copy(mainSave, Path.Combine(latestFolder, Program.saveName), true);
            foreach(var possible in zza_services)
            {
                if (!(possible is SavedService service))
                    continue;
                var from = Path.Combine(Program.BASE_PATH, "Saves", service.SaveFile);
                var to = Path.Combine(latestFolder, service.SaveFile);
                File.Copy(from, to, true);
            }
        }
    }
}
