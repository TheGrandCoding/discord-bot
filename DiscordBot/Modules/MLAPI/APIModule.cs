using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Commands;
using DiscordBot.Commands.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules.MLAPI
{
    [Group("mlapi")]
    [Name("API Module")]
    public class APIModule : BotModule
    {
        [Command("password")]
        [Alias("pwd", "pass")]
        [Summary("Sets your MLAPI password.")]
        public async Task<RuntimeResult> SetPassword([Sensitive][Remainder]string password)
        {
            if(password.Length < 8 || password.Length > 32)
                return new BotResult($"Password must be 8-32 charactors long");
            var leaked = await Program.IsPasswordLeaked(password);
            if (leaked)
                return new BotResult($"Password is known to be compromised; it cannot be used.");

            var t = Context.BotUser.Tokens.FirstOrDefault(x => x.Name == AuthToken.LoginPassword);
            if(t == null)
            {
                t = new AuthToken(AuthToken.LoginPassword, 32);
                Context.BotUser.Tokens.Add(t);
            }
            t.SetHashValue(password);
            Program.Save(); 
            await ReplyAsync("Password has been set!");
            return new BotResult();
        }
    }
}
