using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Classes.CoronAPI;
using DiscordBot.Commands;
using DiscordBot.Services;
using DiscordBot.Utils;
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
    [Name("COVID-19")]
    [Group("covid")]
    [RequireContext(ContextType.Guild)]
    public class CoronaModule : BotBase
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
            var client = Program.Services.GetRequiredService<BotHttpClient>();
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
    
        [Command("isolate"), Alias("self-isolate")]
        [Summary("Marks you as self-isolating")]
        public async Task<RuntimeResult> Isolate(int days = CoronaService.IsolationPeriod)
        {
            if (days < (CoronaService.IsolationPeriod / 2))
                return new BotResult($"Isolation period cannot be less than {CoronaService.IsolationPeriod / 2} days.");
            var usr = Context.User as SocketGuildUser;
            if (usr.Nickname != null && usr.Nickname.StartsWith(Emotes.MICROBE.Name))
                return new BotResult("You are already self-isolating");
            var expire = DateTime.Now.AddDays(days);
            Service.Isolation[Context.User.Id] = expire.ToLastSecond();
            Service.OnSave();
            Service.OnDailyTick();
            return await Success($"Marked you as isolating until {expire:dd/MM/yy hh:mm:ss}");
        }

        [Command("isolate"), Alias("self-isolate")]
        [Summary("Marks a user as self-isolating")]
        [RequireUserPermission(GuildPermission.ManageNicknames)]
        public async Task<RuntimeResult> Isolate(BotUser user, int days = 14)
        {
            var usr = Context.Guild.GetUser(user.Id);
            if(days <= 0)
            {
                if (!Service.Isolation.ContainsKey(usr.Id))
                    return new BotResult("Cannot set a negative day value - they are not isolating");
                Service.Isolation[usr.Id] = DateTime.Now.AddDays(-10);
                Service.OnDailyTick(); // removes nickname and them from list.
                Service.OnSave();
                return new BotResult();
            }
            if (usr.Nickname != null && usr.Nickname.StartsWith(Emotes.MICROBE.Name))
                return new BotResult("They are already self-isolating");
            var expire = DateTime.Now.AddDays(days);
            Service.Isolation[usr.Id] = expire.ToLastSecond();
            Service.OnSave();
            Service.OnDailyTick();
            return await Success($"Marked that user as isolating until {expire:dd/MM/yy hh:mm:ss}");
        }
    }
}
