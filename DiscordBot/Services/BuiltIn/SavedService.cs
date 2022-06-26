using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DiscordBot.Services
{
    public abstract class SavedService : Service
    {
        public static string SaveFolder => Path.Combine(Program.BASE_PATH, "Saves");
        public virtual string SaveFile => Name + ".json";

        public string FullPath => Path.Combine(SaveFolder, SaveFile);

        public virtual string ReadSave(string defaultContent = "{}")
        {
            string s = null;
            try
            {
                s = File.ReadAllText(FullPath, Encoding.UTF8);
            } catch { }
            if (s == "null")
                s = null;
            return s ?? defaultContent;
        }

        public abstract string GenerateSave();

        public override void OnSave()
        {
            var content = GenerateSave();
            DirectSave(content);
        }

        public void DirectSave(string content)
        {
            if (string.IsNullOrWhiteSpace(content) || content == "null")
                return; // refuse to save dat.
            if (!Directory.Exists(SaveFolder))
                Directory.CreateDirectory(SaveFolder);
            File.WriteAllText(FullPath, content);
        }
    }
}
