using DiscordBot.Classes.HTMLHelpers.Objects;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.MLAPI
{
    public class APIEndpoint
    {
        public APIEndpoint(string method, PathAttribute path)
        {
            Method = method;
            m_path = path;
            IsRegex = path.Text.Contains("{");
        }
        public string Method { get; set; }
        public string Name { get; set; }
        public string Summary { get; set; }
        public Dictionary<string, string> Regexs { get; set; } = new Dictionary<string, string>();
        public MethodInfo Function { get; set; }
        public APIPrecondition[] Preconditions { get; set; }
        public APIModule Module { get; set; }
        public bool IsRegex { get; }

        private PathAttribute m_path;
        string getGroupConstruct(string name, string pattern)
        {
            return $"(?<{name}>{pattern})";
        }
        public string GetRegexPattern()
        {
            if (Regexs.TryGetValue(".", out var path))
                return path;
            if (!IsRegex)
                return GetNicePath();
            var sb = new StringBuilder();
            var slices = m_path.Text.Split('/');
            foreach(var x in slices)
            {
                if (x == "")
                    continue;
                sb.Append(@"\/");
                if(x.StartsWith("{"))
                {
                    var name = x[1..^1];
                    var pattern = Regexs[name];
                    var group = getGroupConstruct(name, pattern);
                    sb.Append(group);
                } else
                {
                    sb.Append(x);
                }
            }
            if (sb.Length == 0)
                sb.Append(@"\/");
            return sb.ToString();
        }
        public string GetNicePath() => m_path.Text;

        public bool IsMatch(string path, out Match match)
        {
            match = null;
            if (!IsRegex)
                return GetNicePath() == path;
            var rgx = new Regex(GetRegexPattern());
            match = rgx.Match(path);
            return match?.Success ?? false;
        }

        public string fullInfo()
        {
            string str = $"{Method} {m_path.Text}";
            string suffix = "";
            var paras = Function.GetParameters();
            if (paras.Length > 0)
            {
                suffix = " ";
                foreach (var param in paras)
                {
                    string[] wrappers;
                    if (param.IsOptional)
                    {
                        wrappers = new string[] { "[", "]" };
                    }
                    else
                    {
                        wrappers = new string[] { "<", ">" };
                    }
                    suffix += $"{wrappers[0]}{param.ParameterType} {param.Name}{(param.IsOptional ? $" = {param.DefaultValue}" : "")}{wrappers[1]} ";
                }
                suffix = suffix.Substring(0, suffix.Length - 1);
            }
            return str + suffix;
        }

        string typeName(Type type)
        {
            return type.Name switch
            {
                "System.Int" => "int",
                "System.Boolean" => "bool",
                "System.Double" => "double",
                "System.String" => "string",
                _ => type.Name
            };
        }

        public Div GetDocs()
        {
            var div = new Div(cls: "docs");
            div.Children.Add(new Span(cls: $"docsMethod docsMethod-{Method}")
            {
                RawText = Method
            });
            div.Children.Add(new Span(cls: "docsPath")
            {
                RawText = m_path.Text
            });
            var paramTable = new Table(cls: "docsTable");
            paramTable.Children.Add(new TableRow()
                .WithHeader("Parameter Name")
                .WithHeader("Type")
                .WithHeader("Notes"));
            foreach(var x in Function.GetParameters())
            {
                var row = new TableRow()
                    .WithCell($"<code class='inline'>{x.Name}</code>")
                    .WithCell(typeName(x.ParameterType))
                    .WithCell("");
                if(x.IsOptional)
                {
                    row.Children[0].RawText += "?";
                    row.Children[2].RawText = $"Default: {x.DefaultValue}";
                }
                paramTable.Children.Add(row);
            }
            div.Children.Add(paramTable);
            return div;
        }

        public override string ToString()
        {
            return $"{Method} {m_path.Text}";
        }
    }
}
