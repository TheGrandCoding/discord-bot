using Google.Cloud.Translation.V2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Utils
{
    public static class LanguageCodesUtils
    {
        static Dictionary<string, string> codeToName { get; set; }
        static Dictionary<string, string> nameToCode { get; set; }
        static LanguageCodesUtils()
        {
            nameToCode = new Dictionary<string, string>();
            codeToName = new Dictionary<string, string>();
            var constants = typeof(LanguageCodes).GetFields()
                .Where(x => x.FieldType == typeof(string));
            foreach (var constant in constants)
            {
                var name = constant.Name;
                var code = constant.GetValue(null) as string;
                nameToCode[name.ToLower()] = code;
                codeToName[code] = name;
            }
        }
        public static string ToCode(string name)
        {
            if (nameToCode.TryGetValue(name, out var code))
                return code;
            if (codeToName.ContainsKey(name))
                return name;
            return null;
        }
        public static string ToName(string code)
        {
            var name = codeToName[code];
            return name.Substring(0, 1).ToUpper() + name[1..];
        }
    }
}
