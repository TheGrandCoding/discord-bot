﻿using DiscordBot.Classes.HTMLHelpers.Objects;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class APIEndpoint
    {
        public string Method { get; set; }
        public PathAttribute Path { get; set; }
        public MethodInfo Function { get; set; }
        public APIPrecondition[] Preconditions { get; set; }
        public Type Module { get; set; }

        public string fullInfo()
        {
            string str = $"{Method} {Path.Path}";
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
                RawText = Path.Path
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
            return $"{Method} {Path.Path}";
        }
    }
}