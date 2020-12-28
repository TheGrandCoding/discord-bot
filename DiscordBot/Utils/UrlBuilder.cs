using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Utils
{
    public class UrlBuilder
    {
        public string Base { get; set; }
        Dictionary<string, string> args;

        public static UrlBuilder Discord() => new UrlBuilder("https://discord.com/api/oauth2/authorize")
                                                  .Add("client_id", Program.AppInfo.Id.ToString());

        public UrlBuilder(string domainAndPath)
        {
            Base = domainAndPath;
            args = new Dictionary<string, string>();
        }
        public UrlBuilder Add(string name, string value, bool escape = true)
        {
            if (escape)
                args[Uri.EscapeDataString(name)] = Uri.EscapeDataString(value);
            else
                args[name] = value;
            return this;
        }
        public string this[string key]
        {
            get
            {
                return Uri.UnescapeDataString(args[Uri.EscapeDataString(key)]);
            } set
            {
                Add(key, value);
            }
        }
        public override string ToString()
        {
            string b = Base;
            bool first = false;
            foreach(var pair in args)
            {
                b += (first ? "&" : "?") + pair.Key + "=" + pair.Value;
                first = true;
            }
            return b;
        }

        public static implicit operator string(UrlBuilder b) => b.ToString();
        public static implicit operator Uri(UrlBuilder b) => new Uri(b.ToString());
    }
}
