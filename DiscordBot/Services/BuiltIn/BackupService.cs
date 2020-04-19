﻿using Discord.WebSocket;
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
                Program.LogMsg($"Backing up {files.Count()} save files", Discord.LogSeverity.Verbose, "Backup");
                var zipTemp = Path.Combine(oldFolder, "temp.zip");
                var compressed = GetZipArchive(files.ToList());
                File.WriteAllBytes(zipTemp, compressed);
                File.SetAttributes(zipTemp, FileAttributes.Normal);
                string dateName = Path.Combine(oldFolder, $"{DateTime.Now.ToString("yyyy-MM-dd")}.zip");
                File.Move(zipTemp, dateName, true);
                File.SetAttributes(dateName, FileAttributes.Normal);
            }
            else
            {
                Program.LogMsg("Skipping zip since empty");
            }

            // Step 2: Copy current saves into Latest folder
            var mainSave = Path.Combine(Program.BASE_PATH, Program.saveName);
            string mainTo = Path.Combine(latestFolder, Program.saveName);
            File.SetAttributes(mainSave, FileAttributes.Normal);
            File.Copy(mainSave, mainTo, true);
            File.SetAttributes(mainTo, FileAttributes.Normal);
            foreach(var possible in zza_services)
            {
                if (!(possible is SavedService service))
                    continue;
                var from = Path.Combine(Program.BASE_PATH, "Saves", service.SaveFile);
                var to = Path.Combine(latestFolder, service.SaveFile);
                if(File.Exists(from))
                {
                    File.Copy(from, to, true);
                    File.SetAttributes(to, FileAttributes.Normal);
                }
            }
        }
    }
}
