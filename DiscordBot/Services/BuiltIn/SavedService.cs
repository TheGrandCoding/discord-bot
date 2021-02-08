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

        public virtual string ReadSave(string defaultContent = "{}")
        {
            string s = null;
            try
            {
                s = File.ReadAllText(Path.Combine(SaveFolder, SaveFile), Encoding.UTF8);
            } catch { }
            return s ?? defaultContent;
        }

        public abstract string GenerateSave();

        public override void OnSave()
        {
            var content = GenerateSave();
            OnSave(content);
        }

        public void OnSave(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return; // refuse to save dat.
            if (!Directory.Exists(SaveFolder))
                Directory.CreateDirectory(SaveFolder);
            File.WriteAllText(Path.Combine(SaveFolder, SaveFile), content);
        }
    }
}
