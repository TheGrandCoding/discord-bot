using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    public class EncodeDecodeModule : BotBase
    {
        [Command("encode")]
        [Summary("Encodes the provided message into a special format.")]
        public async Task Encode([Remainder]string encode)
        {
            await ReplyAsync($"```\r\n{Program.ToEncoded(encode)}\r\n```");
        }

        [Command("decode")]
        [Summary("Decodes the provided message from the special format.")]
        public async Task Decode([Remainder]string message)
        {
            await ReplyAsync($"```\r\n{Program.FromEncoded(message)}\r\n```");
        }
    }
}
