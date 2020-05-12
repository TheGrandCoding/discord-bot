using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiscordBot.Classes.Calculator.Functions
{
    public class MethodFunction : Function
    {
        public MethodInfo method;
        public object parent;
        public MethodFunction(Calculator t, MethodInfo f, object obj = null) : base(t, f.Name.ToLower(), f.GetParameters().Select(x => x.Name).ToArray())
        {
            method = f;
            parent = obj;
        }

        protected override double Execute(List<string> args)
        {
            var passing = new List<object>();
            int i = 0;
            foreach(var x in method.GetParameters())
            {
                var r = Program.AttemptParseInput(args[i], x.ParameterType);
                if (!r.IsSuccess)
                    throw new InvalidOperationException($"Could not parse {args[i]}::{Name} as {x.ParameterType.Name}");
                i += 1;
                passing.Add(r.BestMatch);
            }
            var result = method.Invoke(parent, passing.ToArray());
            return Convert.ToDouble(result);
        }
    }
}
