using Discord.Commands;
using DiscordBot.Classes.Calculator;
using DiscordBot.Classes.Calculator.Functions;
using DiscordBot.Commands;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    [Name("Calculator")]
    public class Calculations : BotModule
    {
        public TeXFormatter Service { get; set; }
        [Command("calculate")]
        [Summary("Parses and calculates the given input, eg '3 + 5' = '8'")]
        [Alias("c", "calc", "calculator")]
        public async Task<RuntimeResult> Calculate([Remainder]string input)
        {
            double result;
            var calc = new Calculator();
            try
            {
                result = calc.Output(input);
            } catch (Exception ex)
            {
                return new BotResult($"Failed: {ex.Message}\r\n```\n{string.Join("\n", calc.Steps)}\n```");
            }
            await ReplyAsync($"Answer: **{result}**\r\n```\n{string.Join("\n", calc.Steps)}\n```");
            return new BotResult();
        }

        [Command("clist")]
        [Summary("Lists all method functions available")]
        public async Task ListMethods()
        {
            var sb = new StringBuilder();
            var c = new Calculator();
            foreach(var proc in c.Processes)
            {
                if (!(proc is MethodFunction meth))
                    continue;
                var args = meth.method.GetParameters()
                    .Select(x => $"*{x.ParameterType.Name}* `{x.Name}`");
                sb.Append($"*{meth.method.ReturnType.Name}* **{meth.Name}**({string.Join(", ", args)})\n");
            }
            await ReplyAsync(sb.ToString());
        }
    
        [Command("display")]
        [Alias("tex", "show")]
        [Summary("Transforms provided TeX string into image for display")]
        public async Task ShowTeX([Remainder]string tex)
        {
            await Service.ReplyTeXMessage(tex, Context.Message);
        }
    }
}
