using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace DiscordBot.Services.BuiltIn
{
    public class BackupService : Service
    {
        public override bool IsCritical => true;

        public static byte[] GetZipArchive(List<InMemoryFile> files)
        {
            byte[] archiveFile;
            using (var archiveStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
                {
                    foreach (var file in files)
                    {
                        var zipArchiveEntry = archive.CreateEntry(file.FileName, CompressionLevel.Fastest);
                        using (var zipStream = zipArchiveEntry.Open())
                            zipStream.Write(file.Content, 0, file.Content.Length);
                    }
                }

                archiveFile = archiveStream.ToArray();
            }

            return archiveFile;
        }

        public class InMemoryFile
        {
            public string FileName { get; set; }
            public byte[] Content { get; set; }
            public InMemoryFile(string f)
            {
                FileName = Path.GetFileName(f);
                Content = File.ReadAllBytes(f);
            }
        }

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
            var files = Directory.GetFiles(latestFolder).Select(x => new InMemoryFile(x));
            if(files.Count() > 0)
            {
                var zipTemp = Path.Combine(oldFolder, "temp.zip");
                Program.LogMsg($"Creating zip; from: {latestFolder}, to: {zipTemp}");
                Program.LogMsg($"Files to backup:");
                foreach (var x in files)
                    Program.LogMsg($"{x.FileName}: {x.Content.Length / 1000}kB");
                var compressed = GetZipArchive(files.ToList());
                Program.LogMsg($"Writing {compressed.Length / 1000}kB to file: {zipTemp}");
                File.WriteAllBytes(zipTemp, compressed);
                Program.LogMsg($"File wrote!");
                File.Move(zipTemp, Path.Combine(oldFolder, $"{DateTime.Now.ToString("yyyy-MM-dd")}.zip"), true);
                File.SetAttributes(zipTemp, FileAttributes.Normal);
            } else
            {
                Program.LogMsg("Skipping zip since empty");
            }

            // Step 2: Copy current saves into Latest folder
            var mainSave = Path.Combine(Program.BASE_PATH, Program.saveName);
            File.Copy(mainSave, Path.Combine(latestFolder, Program.saveName), true);
            File.SetAttributes(mainSave, FileAttributes.Normal);
            foreach(var possible in zza_services)
            {
                if (!(possible is SavedService service))
                    continue;
                var from = Path.Combine(Program.BASE_PATH, "Saves", service.SaveFile);
                var to = Path.Combine(latestFolder, service.SaveFile);
                if(File.Exists(from))
                {
                    Program.LogMsg($"Copying for {service.Name}; {File.GetAttributes(from)}");
                    File.Copy(from, to, true);
                    File.SetAttributes(to, FileAttributes.Normal);
                }
            }
        }
    }
}
