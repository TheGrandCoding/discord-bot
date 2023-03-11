using Discord;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DiscordBot.Services.BuiltIn
{
    public class ConfigUpdateService : Service
    {
        public override void OnReady(IServiceProvider services)
        {
#if !DEBUG
            return;
#endif
            var json = new JObject();
            Save(json, Program.Configuration);
            var str = json.ToString(Newtonsoft.Json.Formatting.Indented);
            var aa = Environment.GetCommandLineArgs();
            // "D:\\_GitHub\\discord-bot\\DiscordBot\\bin\\Debug\\netcoreapp3.1\\DiscordBot.dll"
            var folder = new FileInfo(aa[0]).Directory;
            // "D:\\_GitHub\\discord-bot\\DiscordBot\\bin\\Debug\\netcoreapp3.1"
            folder = folder.Parent.Parent.Parent.Parent;
            if (folder.GetFiles().Any(x => x.Name == "README.md") == false)
                throw new InvalidOperationException("Path structure has changed.");
            var path = Path.Combine(folder.FullName, "_configuration.example.json");
            File.WriteAllText(path, str);
        }
        void Save(JObject json, IConfiguration config)
        {
            foreach(var child in config.GetChildren())
            {
                if(child.Value != null)
                {
                    json[child.Key] = getValueKind(child.Value);
                } else
                {
                    var jobj = new JObject();
                    Save(jobj, child);
                    json[child.Key] = jobj;
                }
            }
        }
        string getValueKind(string value)
        {
            if (int.TryParse(value, out _))
                return "<int>";
            if (ulong.TryParse(value, out _))
                return "<ulong>";
            if (bool.TryParse(value, out _))
                return "<bool>";
            if (value.Contains("://") && Uri.TryCreate(value, UriKind.Absolute, out _))
                return "<uri>";
            if (tryEnum<LogSeverity>(value, out var s))
                return s;
            return $"<string>";
        }
        bool tryEnum<T>(string input, out string result) where T:struct
        {
            result = null;
            if(Enum.TryParse<T>(input, true, out _))
            {
                result = $"<{string.Join("|", Enum.GetNames(typeof(T)))}>";
                return true;
            }
            return false;
        }
    }
}