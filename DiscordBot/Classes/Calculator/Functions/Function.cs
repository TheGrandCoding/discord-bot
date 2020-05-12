using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator.Functions
{
    public abstract class Function : CalcProcess
    {
        public Function(Calculator t, string name, params string[] argNames) : base(t)
        {
            Name = name;
            Arguments = argNames.ToList();
        }
        public string Name { get; }
        public List<string> Arguments { get; }
        protected override string RegStr
        {
            get
            {
                var text = $@"{Name}\(";
                foreach(var arg in Arguments)
                {
                    text += DOUBLE + ",";
                }
                if(Arguments.Count > 0)
                    text = text.Substring(0, text.Length - 1); // strip trailing ,
                text += @"\)";
                return text;
            }
        }
        public override double Process(string input, Match m)
        {
            var param = new List<string>();
            if ((m.Groups.Count - 1) != Arguments.Count)
                throw new InvalidOperationException($"Not enough arguments given: " +
                    $"{Name}({string.Join(", ", Arguments)})");
            int i = 1;
            foreach(var x in Arguments)
            {
                param.Add(m.Groups[i].Value);
                i += 1;
            }

            return Execute(param);
        }
        protected abstract double Execute(List<string> args);
        protected TArg parse<TArg>(string inp)
        {
            var r = Program.AttemptParseInput<TArg>(inp);
            if (!r.IsSuccess)
                throw new InvalidOperationException($"Could not parse '{inp}' for {typeof(TArg).Name} argument for {Name}: {r.ErrorReason}");
            return (TArg)r.BestMatch;
        }
    }

    public abstract class Function<T1> : Function
    {
        public Function(Calculator t, string name, string arg1) : base(t, name, arg1) { }
        protected override double Execute(List<string> args)
        {
            return Execute(parse<T1>(args[0]));
        }
        public abstract double Execute(T1 arg1);
    }

    public abstract class Function<T1, T2> : Function
    {
        public Function(Calculator t, string name, string arg1, string arg2) : base(t, name, arg1, arg2) { }
        protected override double Execute(List<string> args)
        {
            return Execute(
                parse<T1>(args[0]),
                parse<T2>(args[0])
                );
        }
        public abstract double Execute(T1 arg1, T2 arg2);
    }
}
