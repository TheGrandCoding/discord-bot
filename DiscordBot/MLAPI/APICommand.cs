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

        public override string ToString()
        {
            return $"{Method} {Path.Path}";
        }
    }
}
