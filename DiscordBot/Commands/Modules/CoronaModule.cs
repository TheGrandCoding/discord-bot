using Discord;
using Discord.Commands;
using Discord.Rest;
using DiscordBot.Classes.CoronAPI;
using DiscordBot.Commands;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Name("COVID-19 Statistics")]
    [Group("covid")]
    [RequireContext(ContextType.Guild)]
    public class CoronaModule : BotModule
    {
        public CoronaService Service { get; set; }

        [Command("register")]
        [Summary("Creates a channel for the bot to send stats into")]
        [RequireBotPermission(Discord.ChannelPermission.ManageChannels)]
        [RequireUserPermission(Discord.GuildPermission.ManageGuild)]
        public async Task<RuntimeResult> Register(string code)
        {
            var anyInGuild = Service.Entries.Where(x => x.Channel.Guild.Id == Context.Guild.Id);
            var prevChannel = anyInGuild.FirstOrDefault()?.Channel;
            var existing = anyInGuild.FirstOrDefault(x => x.Code == code);
            if(existing != null)
                return new BotResult($"`{code}` is already registered in this server: " +
                    $"{(existing.Message?.GetJumpUrl() ?? existing.Channel.Mention)}");
            var client = Program.Services.GetRequiredService<HttpClient>();
            var response = await client.GetAsync(CoronaService.URL + code);
            var text = await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode)
            {
                var jobj = JObject.Parse(text);
                return new BotResult($"Failed to fetch data: `{jobj["message"]}`");
            }
            var data = Program.Deserialise<CoronaResponse>(text);
            ITextChannel c = prevChannel ?? await Context.Guild.CreateTextChannelAsync("🦠COVID-19");
            if(c is RestTextChannel)
                await c.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole, Program.ReadPerms);
            var m = await c.SendMessageAsync(embed: Service.getEmbed(data.Data).Build());
            var obj = new SendingEntry()
            {
                Code = code,
                Channel = c,
                Message = m
            };
            Service.Entries.Add(obj);
            Service.OnSave();
            await ReplyAsync($"Registered to {c.Mention}");
            return new BotResult();
        }
    
        [Command("search")]
        [Summary("Searches for a country's code")]
        public async Task Search(string input)
        {
            var cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);
            foreach(var x in cultures)
            {
                if(x.DisplayName.Contains(input, StringComparison.OrdinalIgnoreCase))
                {
                    await ReplyAsync($"Add: `{Program.Prefix}covid register {x.TwoLetterISOLanguageName}`\r\n" +
                        $"\r\n>>> {x.DisplayName}");
                    return;
                }
            }
            await ReplyAsync("Could not find any countries by that input.");
        }
    
    }
}
