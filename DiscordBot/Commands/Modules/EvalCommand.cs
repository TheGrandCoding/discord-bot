using Discord;
using Discord.Commands;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CSharp;
using Newtonsoft.Json.Linq;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [RequireOwner]
    public class EvalCommand : BotBase
    {
        [Command("eval")]
        public async Task Eval([Remainder]string input)
        {
            input = input.Trim();
            if(input.StartsWith("```"))
            {
                input = input.Substring(input.IndexOf("\n"));
            }
            if(input.EndsWith("```"))
            {
                input = input.Substring(0, input.Length - "```".Length);
            }
            input = input.Trim();

            if (input.Contains("return") == false)
                input = "return " + input;
            if (!input.EndsWith(";"))
                input += ";";

            string code = EvalCommand.sourceCode.Replace("{0}", input);

            
            var ns = Assembly.Load("netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

            var references = new List<MetadataReference>()
            {
                MetadataReference.CreateFromFile(ns.Location), //netstandard
                MetadataReference.CreateFromFile(typeof(Object).Assembly.Location), //mscorlib
                MetadataReference.CreateFromFile(typeof(Action).Assembly.Location), //System.Runtime
                MetadataReference.CreateFromFile(typeof(EvalCommand).Assembly.Location), // this assembly
                MetadataReference.CreateFromFile(typeof(Discord.IDiscordClient).Assembly.Location),
            };

            foreach (AssemblyName names in Assembly.GetExecutingAssembly().GetReferencedAssemblies())
            {
                var ass = Assembly.Load(names);
                var data = MetadataReference.CreateFromFile(ass.Location);
                references.Add(data);
            }


            var comp = CSharpCompilation.Create(
                assemblyName: Path.GetRandomFileName(),
                syntaxTrees: new[] { CSharpSyntaxTree.ParseText(code) },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            using (var ms = new MemoryStream())
            {
                var result = comp.Emit(ms);
                if (!result.Success)
                {
                    var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                    var embed = new EmbedBuilder();
                    embed.Title = "Compile Error";
                    embed.Color = Color.Red;
                    foreach (Diagnostic diagnostic in failures)
                    {
                        embed.AddField(diagnostic.Id + " " + diagnostic.Location.GetLineSpan().StartLinePosition.Line, Program.Clamp(diagnostic.GetMessage(), 256));
                    }
                    await ReplyAsync(embed: embed.Build());
                    return;
                }
                ms.Seek(0, SeekOrigin.Begin);
                Assembly assembly = Assembly.Load(ms.ToArray());

                // create instance of the desired class and call the desired function
                Type type = assembly.GetType("DiscordBot.EvaluateCommand.EvalCmdExecute");
                var tsk = type.InvokeMember("Execute",
                    BindingFlags.Default | BindingFlags.InvokeMethod,
                    null,
                    null,
                    new object[] { Context }) as Task<object>;
                var rtn = tsk.Result;
                string s = toString(rtn);
                var path = Path.Combine(Path.GetTempPath(), "output.json");
                File.WriteAllText(path, s);
                await Context.Channel.SendFileAsync(path);
            }
        }

        string toString(object o)
        {
            if (o == null)
                return "<null>";
            return getJson(o, new List<string>()).ToString(Newtonsoft.Json.Formatting.Indented);
        }

        string getDupeId(object o)
        {
            if (o is ISnowflakeEntity e)
                return $"{e.Id}";
            return $"{o.GetType().Name}:{o.GetHashCode()}";
        }

        JToken getJson(object o, List<string> dupeChecker, int depth = 0)
        {
            if (o == null)
                return JValue.CreateNull();
            var type = o.GetType();
            if (type.IsPrimitive)
                return JToken.FromObject(o);
            if (o is string s)
                return JToken.FromObject(s);
            if (type.Namespace == "System")
                return JToken.FromObject(o.ToString());
            if(depth > 3 || dupeChecker.Contains(getDupeId(o)))
            {
                Console.WriteLine(new String(' ', depth * 2) + " Duplicate or depth exceeded");
                var exx = new JObject();
                exx["_type"] = type.Name;
                exx["_str"] = o.ToString();
                return exx;
            }
            dupeChecker.Add(getDupeId(o));
            if(type.IsArray || typeof(IEnumerable).IsAssignableFrom(type))
            {
                var jarr = new JArray();
                Console.WriteLine(new String(' ', depth * 2) + " Examaning array " + type.Name);
                foreach(var obj in (IEnumerable)o)
                {
                    jarr.Add(getJson(obj, dupeChecker, depth + 1));
                }
                return jarr;
            }
            if(type.IsEnum)
            {
                Console.WriteLine(new String(' ', depth * 2) + " Enum " + type.Name);
                return JToken.FromObject((int)o);
            }
            var jobj = new JObject();
            Console.WriteLine(new String(' ', depth * 2) + " Examaning object " + type.Name);
            foreach(var property in type.GetProperties())
            {
                Console.WriteLine(new String(' ', depth * 2) + " -> " + property.Name);
                JToken propObj;
                try
                {
                    propObj = getJson(property.GetValue(o), dupeChecker, depth + 1);
                } catch(Exception ex)
                {
                    Program.LogError(ex, type.Name + ":" + property.Name + ":" + depth.ToString());
                    var errObj = new JObject();
                    errObj["_err"] = ex.Message;
                    propObj = errObj;
                }
                jobj[property.Name] = propObj;
            }
            Console.WriteLine(new String(' ', depth * 2) + " Done " + type.Name);
            return jobj;
        }

        const string sourceCode = @"using System;
using System.Collections;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DiscordBot.EvaluateCommand 
{
    public static class EvalCmdExecute 
    {
        public static async Task<object> Execute(ICommandContext Context) 
        {
            {0}
            return Task.CompletedTask;
        }
    }
}";
    }
}
