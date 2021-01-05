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

namespace DiscordBot.Commands.Modules
{
    [Name("Calculator")]
    public class Calculations : BotBase
    {
        public TeXFormatter Service { get; set; }
        [Command("calculate")]
        [Summary("Parses and calculates the given input, eg '3 + 5' = '8'")]
        [Alias("c", "calc", "calculator")]
        public async Task<RuntimeResult> Calculate([Remainder]string input)
        {
            Result result;
            var calc = new CalculationTree(input);
            try
            {
                result = calc.Calculate();
            } catch (Exception ex)
            {
                return new BotResult($"Failed: {ex.Message}\r\n```\n{string.Join("\n", calc.CalcLog)}\n```");
            }
            await ReplyAsync($"Answer: **{result}**\r\n```\n{string.Join("\n", calc.CalcLog)}\n```");
            return new BotResult();
        }

        [Command("clist")]
        [Summary("Lists all method functions available")]
        public async Task ListMethods()
        {
            var sb = new StringBuilder();
            foreach(var meth in CalculationTree.Functions)
            {
                var args = meth.GetParameters()
                    .Select(x => $"*{x.ParameterType.Name}* `{x.Name}`");
                sb.Append($"*{meth.ReturnType.Name}* **{meth.Name}**({string.Join(", ", args)})\n");
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
