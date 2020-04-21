using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Services.BuiltIn;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    public class Docs : APIBase
    {
        public Docs(APIContext c) : base(c, "bot")
        {
        }
        private string msgBox(string type, string message)
        {
            return $"<div class='msgBox {type}'>" +
                $"<p>{message}</p>" +
                $"</div>";
        }
        private string info(string message) => msgBox("info", message);
        private string warn(string message) => msgBox("warn", message);

        string CurrentLook { get; set; }

        string getListInfo(ModuleInfo module, int level)
        {
            // assume we're already in an OL, just set correct indent and headers
            string text = $"<li class='level-{level}'>" +
                $"{aLink("/docs/cmd/" + module.Name.Replace(" ", "."), module.Name)}</li>";
            foreach (var child in module.Submodules)
                text += getListInfo(child, level + 1);
            return text;
        }

        string getSidebarCommands()
        {
            string text = "";
            text += "<ol>";
            foreach(var mod in Program.Commands.Modules)
            {
                text += getListInfo(mod, 0);
            }
            return text + "</ol>";
        }

        string getSidebarAPI()
        {
            string text = "<ol>";

            var modules = new Dictionary<string, Type>();
            foreach(var list in Handler.Endpoints.Values)
            {
                foreach(var cmd in list)
                {
                    modules.TryAdd(cmd.Module.Name, cmd.Module);
                }
            }

            foreach(var keypair in modules)
            {
                string url = keypair.Key;
                var mod = keypair.Value;
                text += $"<li class='level-0'>" +
                    $"{aLink("/docs/api/" + url, mod.Name)}" +
                    $"</li>";
            }

            return text + "</ol>";
        }

        Replacements rep()
        {
            return new Replacements()
                .Add("commands", getSidebarCommands())
                .Add("modules", getSidebarAPI());
        }

        string span(string cls, string content) => $"<span class='{cls}'>{content}</span>";
        string getInfo(ParameterInfo param)
        {
            string text = " ";
            text += param.IsOptional ? span("paramo", "[") : span("paramr", "&lt;");
            text += span("paramtype", param.Type.Name);
            text += param.IsOptional
                ? span("paramname paramo", " " + param.Name)
                : span("paramname paramr", " " + param.Name);
            text += param.IsOptional ? span("paramo", "]") : span("paramr", "&gt;");
            return text;
        }
        string getInfo(CommandInfo cmd)
        {
            string text = "<div class='cmd'>";
            text += $"<code>{Program.Prefix}{cmd.Aliases.First()}";
            foreach (var param in cmd.Parameters)
                text += getInfo(param);
            text += "</code>";
            text += getInfoBoxes(cmd);
            text += $"<p>{(string.IsNullOrWhiteSpace(cmd.Summary) ? "No summary" : cmd.Summary)}</p>";
            return text + "</div>";
        }
        static CmdDisableService disable;
        string getInfoBoxes(ModuleInfo module)
        {
            string text = "";
            disable ??= Program.Services.GetRequiredService<CmdDisableService>();
            if(disable.IsDisabled(module, out var reason))
            {
                text += $"<div class='msgBox error'><p><strong>This module has been disabled</strong></p>" +
                    $"<p>{reason}</p>";
            }
            foreach(var x in module.Attributes)
            {
                if (x is DocBoxAttribute at)
                    text += at.HTML();
            }
            return text;
        }
        string getInfoBoxes(CommandInfo cmd)
        {
            string text = "";
            disable ??= Program.Services.GetRequiredService<CmdDisableService>();
            if (disable.IsDisabled(cmd, out var reason))
            {
                text += $"<div class='msgBox error'><p><strong>This command has been disabled</strong></p>" +
                    $"<p>{reason}</p>";
            }
            foreach (var x in cmd.Attributes)
            {
                if (x is DocBoxAttribute at)
                    text += at.HTML();
            }
            return text;
        }
        string getInfo(ModuleInfo module)
        {
            string text = $"<h1>{module.Name}</h1>";
            text += getInfoBoxes(module);
            var groups = module.Aliases.Where(x => !string.IsNullOrWhiteSpace(x));
            if (groups.Count() > 0)
            {
                text += $"<p>Prefixes: ";
                foreach (var x in groups)
                    text += $"<code>{Program.Prefix}{x}</code>";
                text += "</p>";
            }
            if(module.Preconditions.Count > 0)
            {
                text += $"<p>Preconditions that apply for every command: <ol>";
                var grouped = module.Preconditions.GroupBy(x => x.Group);
                bool key = grouped.Count() > 1;
                foreach(var grouping in grouped)
                {
                    foreach(var item in grouping)
                    {
                        text += "<li>";
                        if (key) text += $"<strong>{grouping.Key}</strong> ";
                        text += $"{item.GetType().Name}</li>";
                    }
                }
                text += "</ol>";
            }
            foreach (var cmd in module.Commands)
                text += getInfo(cmd);
            return text;
        }

        [Method("GET"), Path("/docs")]
        public void Base()
        {
            ReplyFile("docs.html", 200, rep()
                .Add("content", "<strong>Please select an item on the left!</strong>"));
        }

        [Method("GET"), PathRegex(@"\/docs\/cmd\/(?<name>[\S\._]+)")]
        public void CommandItem(string name)
        {
            CurrentLook = name;
            var item = Program.Commands.Modules.FirstOrDefault(x => x.Name == name.Replace(".", " "));
            ReplyFile("docs.html", 200, rep()
                .Add("content", getInfo(item)));
        }

        [Method("GET"), PathRegex(@"\/docs\/api\/(?<name>[\S\._]+)")]
        public void APIItem(string name)
        {
            CurrentLook = name;
            ReplyFile("docs.html", 200, rep()
                .Add("content", new DocBoxAttribute("error", "API Documentation not yet implemented").HTML()));
        }

    }
}
