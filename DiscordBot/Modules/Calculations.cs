using Discord.Commands;
using DiscordBot.Classes.Calculator;
using DiscordBot.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    [Name("Calculator")]
    public class Calculations : BotModule
    {
        [Command("calculate")]
        [Summary("Parses and calculates the given input, eg '3 + 5' = '8'")]
        [Alias("c", "calc", "calculator")]
        public async Task<RuntimeResult> Calculate([Remainder]string input)
        {
            string result;
            var calc = new Calculator();
            try
            {
                result = calc.Output(input);
            } catch 
            {
                return new BotResult($"Failed:\r\n```\n{string.Join("\n", calc.Steps)}\n```");
            }
            await ReplyAsync($"Answer: **{result}**\r\n```\n{string.Join("\n", calc.Steps)}\n```");
            return new BotResult();
        }
    }
}
