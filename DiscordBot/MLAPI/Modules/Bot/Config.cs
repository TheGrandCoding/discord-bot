using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.Bot
{
    [RequireOwner]
    public class Config : AuthedAPIBase
    {
        public Config(APIContext context) : base(context, "/bot")
        {
        }

        object parse(string value)
        {
            if (ulong.TryParse(value, out var u))
                return u;
            if (int.TryParse(value, out var i))
                return i;
            if (bool.TryParse(value, out var b))
                return b;
            if (double.TryParse(value, out var d))
                return d;
            return value;
        }

        Span getSpan(string str, string id = null)
        {
            var span = new Span(id: id);
            var value = parse(str);
            if (value is ulong u)
            {
                span.RawText = $"{u}";
                span.Class = "bigint";
            }
            else if (value is int i)
            {
                span.RawText = $"{i}";
                span.Class = "integer";
            }
            else if (value is double d)
            {
                span.RawText = $"{d}";
                span.Class = "double";
            }
            else if (value is bool b)
            {
                span.RawText = $"{b}";
                span.Class = "boolean";
            }
            else
            {
                span.RawText = $"\"{value}\"";
                span.Class = "string";
            }
            span.OnClick = "clickSpan(event);";
            return span;
        }

        HTMLBase getControl(string s) => new RawText(s);

        Div getHtml(IConfigurationSection section)
        {
            var div = new Div(id: section.Path);
            div.Children.Add(getSpan(section.Key));
            div.Children.Add(getControl(": "));
            div.OnMouseOver = "return mouseEnter(event);";
            div.OnMouseOut = "return mouseLeave(event);";
            if (section.Value == null)
            { // we're an object, containing other keypairs.
                // so we want to print something like:
                //     "name": {
                //                  "child1": "value"
                //             }

                div.Children.Add(getControl("{"));

                foreach (var child in section.GetChildren())
                    div.Children.Add(getHtml(child));

                div.Children.Add(getControl("}"));
            } else
            {
                // we're a raw value, so we'll just do that
                div.Children.Add(getSpan(section.Value, section.Path));
            }
            div.Children.Add(getControl(","));
            return div;
        }

        HTMLBase getHtml(IConfigurationRoot root)
        {
            var div = new Div(id: "root", cls: "root");
            div.Children.Add(getControl("{"));

            foreach(var current in root.GetChildren())
            {
                var child = getHtml(current);
                div.Children.Add(child);
            }

            div.Children.Add(getControl("}"));
            return div;
        }

        JToken getJson(IConfigurationSection section)
        {
            if(section.Value == null)
            {
                var jobj = new JObject();
                foreach (var c in section.GetChildren())
                    jobj[c.Key] = getJson(c);
                return jobj;
            }
            return JToken.FromObject(parse(section.Value));
        }

        JObject getJson(IConfigurationRoot root)
        {
            var jobj = new JObject();
            foreach(var current in root.GetChildren())
            {
                jobj[current.Key] = getJson(current);
            }
            return jobj;
        }

        [Method("GET"), Path("/bot/config")]
        public async Task GetConfig()
        {
            var html = getHtml(Program.Configuration);
            var json = getJson(Program.Configuration);
            await ReplyFile("config.html", 200, new Replacements()
                .Add("json", json)
                .Add("html", html));
        }

        [Method("POST"), Path("/bot/config")]
        public async Task NewConfig()
        {
            var json = JObject.Parse(Context.Body);
            var configProvider = Program.Configuration.Providers.First() as JsonConfigurationProvider;
            var source = configProvider.Source;
            var current = new FileInfo(source.FileProvider.GetFileInfo(source.Path).PhysicalPath);
            var backup = Path.Combine(current.Directory.FullName, "_configuration_backup_" + DateTimeOffset.Now.ToUnixTimeSeconds().ToString() + ".json");
            File.Copy(current.FullName, backup, true);
            File.WriteAllText(current.FullName, Context.Body);
            Program.Configuration.Reload();
            await RespondRaw("OK", 200);
        }
    }
}
