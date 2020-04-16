﻿using Discord.Commands;
using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.MLAPI
{
    [Group("mlapi")]
    public class APIModule : BotModule
    {
        [Command("password")]
        [Alias("pwd", "pass")]
        [Summary("Sets your MLAPI password.")]
        public async Task SetPassword([Remainder]string password)
        {
            if(password.Length < 8 || password.Length > 32)
                throw new ArgumentException($"Password must be 8-32 charactors long");
            var t = Context.BotUser.Tokens.FirstOrDefault(x => x.Name == AuthToken.LoginPassword);
            if(t == null)
            {
                t = new AuthToken(AuthToken.LoginPassword, 32);
                Context.BotUser.Tokens.Add(t);
            }
            t.Value = password;
            Program.Save(); 
            await ReplyAsync("Password has been set!");
        }
    }
}
