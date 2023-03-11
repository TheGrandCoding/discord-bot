using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.MLAPI.Attributes;
using DiscordBot.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.MLAPI
{
    public class APIEndpoint
    {
        public APIEndpoint(string method, IEnumerable<PathAttribute> paths, HostAttribute host)
        {
            Method = method;
            var arr = paths.ToArray();
            if(arr.Length == 1)
            {
                m_path = arr[0];
            } else
            {
                var sb = new StringBuilder();
                foreach(var path in paths.Reverse())
                {
                    if(path.Text.StartsWith("/api/"))
                    {
                        sb.Insert(0, "/api");
                        sb.Append(path.Text.Substring("/api".Length));
                    } else
                    {
                        sb.Append(path.Text);
                    }
                }
                if (sb.Length > 1 && sb[sb.Length - 1] == '/')
                    sb.Remove(sb.Length - 1, 1);
                m_path = new PathAttribute(sb.ToString());
            }

            IsRegex = m_path.Text.Contains("{");
            m_host = host;
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
        private HostAttribute m_host { get; set; }
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
        public string GetFormattablePath(bool withQuery = true)
        {
            var paras = Function.GetParameters();
            var path = m_path.Text;
            int i = 0;
            var http = new UrlBuilder(path);
            foreach(var pra in paras)
            {
                if (pra.GetCustomAttribute<FromQueryAttribute>() != null) continue;
                if(pra.GetCustomAttribute<FromBodyAttribute>() != null) continue;
                var bef = "{" + pra.Name + "}";
                var text = "{" + i.ToString() + "}";
                if(http.Base.Contains(bef))
                {
                    http.Base = http.Base.Replace("{" + pra.Name + "}", text);
                } else if(withQuery)
                {
                    http.Add(pra.Name, text, escape: false);
                }
                i++;
            }
            return http;
        }

        public bool IsMatch(string path, string host, out Match match)
        {
            match = null;
            if (m_host != null)
            {
                if (!m_host.IsMatch(host))
                    return false;
            }
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
                var row = new TableRow();
                row.Children.Add(new Code(x.Name, cls: "inline"));
                row.WithCell(typeName(x.ParameterType));
                row.WithCell("");
                if (Nullable.GetUnderlyingType(x.ParameterType) != null)
                {
                    row.Children[0].RawText = "?" + row.Children[0].RawText;
                }
                if (x.IsOptional)
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
