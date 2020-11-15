using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBot.Classes.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    [RequireService(typeof(MsgService))]
    public class LoggingService : SavedService
    {
        public IGuild LogGuild { get; set; }

        Dictionary<ulong, guildSave> GuildMap { get; set; } = new Dictionary<ulong, guildSave>();

        public override string GenerateSave()
        {
            var sv = new logSave()
            {
                guildId = LogGuild?.Id,
                guilds = GuildMap
            };
            return Program.Serialise(sv);
        }

        public override void OnReady()
        {
            var sv = ReadSave();
            var lg = Program.Deserialise<logSave>(sv);
            GuildMap = lg.guilds ?? new Dictionary<ulong, guildSave>();
            if(lg.guildId.HasValue)
            {
                LogGuild = Program.Client.GetGuild(lg.guildId.Value);
                foreach(var guild in Program.Client.Guilds)
                {
                    if (guild.Name == "Logging Guild" && guild.Id != LogGuild.Id)
                        guild.DeleteAsync().Wait();
                }
            } else
            {
                LogGuild = Program.Client.Guilds.FirstOrDefault(x => x.Name == "Logging Guild");
                if (LogGuild == null)
                {
                    var regions = Program.Client.GetOptimalVoiceRegionAsync().Result;
                    LogGuild = Program.Client.CreateGuildAsync("Logging Guild", regions).Result;
                    LogGuild.CreateRoleAsync("Log Master", new GuildPermissions(administrator: true), isMentionable:false).Wait();
                }
                OnSave();
            }
        }

        public override void OnLoaded()
        {
            Program.Client.MessageDeleted += Client_MessageDeleted;
            Program.Client.MessageUpdated += Client_MessageUpdated;
            Program.Client.UserJoined += Client_UserJoined;
        }

        public override void OnDailyTick()
        {
            CheckPermsAll().Wait();
        }

        #region Permissions
        private async Task Client_UserJoined(SocketGuildUser arg)
        {
            if (arg.Guild.Id != LogGuild.Id)
                return;
            await CheckPermsFor(arg);
        }

        public async Task CheckPermsFor(IGuildUser user)
        {
            foreach(var key in GuildMap.Keys)
            {
                var guild = Program.Client.GetGuild(key);
                var save = GuildMap[key];
                var hasPerms = guild.GetUser(user.Id)?.GuildPermissions.Administrator ?? false;
                if (hasPerms)
                    await user.AddRoleAsync(save.Role);
                else
                    await user.RemoveRoleAsync(save.Role);
            }
        }
        public async Task CheckPermsAll()
        {
            foreach(var user in await LogGuild.GetUsersAsync())
            {
                if (user.IsBot)
                    continue;
                await CheckPermsFor(user);
            }
        }
        #endregion

        #region Fetching or creation channels

        public bool isDirty = false;

        async Task createCategory(IGuild guild, guildSave guildInfo)
        {
            guildInfo.Category = await LogGuild.CreateCategoryAsync(guild.Name);
            guildInfo.Role = await LogGuild.CreateRoleAsync(guild.Name, GuildPermissions.None, isMentionable:false);
            await guildInfo.Category.AddPermissionOverwriteAsync(LogGuild.EveryoneRole,
                new OverwritePermissions(viewChannel: PermValue.Deny, sendMessages: PermValue.Deny));
            await guildInfo.Category.AddPermissionOverwriteAsync(guildInfo.Role,
                new OverwritePermissions(viewChannel: PermValue.Allow));
        }

        public async Task<ICategoryChannel> GetCategory(IGuild guild)
        {
            if (GuildMap.TryGetValue(guild.Id, out var guildInfo))
            {
                if(guildInfo.Category == null)
                {
                    await createCategory(guild, guildInfo);
                    isDirty = true;
                } else if (guildInfo.Category.Name != guild.Name)
                {
                    await guildInfo.Category.ModifyAsync(x => x.Name = guild.Name);
                }
                return guildInfo.Category;
            }
            var map = new guildSave()
            {
                Actions = new Dictionary<string, ITextChannel>()
            };
            await createCategory(guild, map);
            GuildMap[guild.Id] = map;
            isDirty = true;
            return map.Category;
        }
        public async Task<ITextChannel> GetChannel(IGuild guild, string action)
        {
            if(GuildMap.TryGetValue(guild.Id, out var guildSave))
            {
                if (guildSave.Actions.TryGetValue(action, out var txt))
                    return txt;
            }
            var category = await GetCategory(guild);
            var sv = GuildMap[guild.Id];
            var newtxt = await LogGuild.CreateTextChannelAsync("log-" + action, x =>
            {
                x.CategoryId = category.Id;
                x.Topic = "Logs for any new, updated or deleted " + action + "s";
            });
            await newtxt.SyncPermissionsAsync();
            sv.Actions[action] = newtxt;
            isDirty = true;
            return newtxt;
        }
        #endregion

        #region Helper Functions
        public async Task<IUserMessage> SendLog(IGuild guild, string action, EmbedBuilder builder, ulong? context = null)
        {
            builder.WithCurrentTimestamp();
            if(context.HasValue)
            {
                var footer = builder.Footer ?? new EmbedFooterBuilder();
                footer.Text = (footer.Text == null ? "" : footer.Text + " ") + context.ToString();
                builder.Footer = footer;
            }
            var chnl = await GetChannel(guild, action);
            var msg = await chnl.SendMessageAsync(embed: builder.Build());
            return msg;
        }
        #endregion

        #region Messages
        private async Task Client_MessageDeleted(Cacheable<IMessage, ulong> arg1, ISocketMessageChannel arg2)
        {
            if (!(arg2 is ITextChannel txt))
                return;
            var service = Program.Services.GetRequiredService<MsgService>();
            var content = service.GetLatestContent(arg1.Id);
            var dbMsg = await service.GetMessageAsync(arg1.Id);
            if (dbMsg == null)
                return;
            var builder = new EmbedBuilder()
                .WithTitle("Message Deleted")
                .WithColor(Color.Red)
                .WithDescription(content?.Content ?? "[unknown last content]");
            builder.AddField("Channel", $"{txt.Mention}", true);
            builder.AddField("Author", $"{dbMsg.Author.Id}\r\n<@{dbMsg.Author.Id}>", true);
            builder.AddField("Original Sent", SnowflakeUtils.FromSnowflake(arg1.Id).ToString("dd/MM/yy HH:mm:ss.fff"), true);
            await SendLog(txt.Guild, "messages", builder, arg1.Id);
            if (isDirty)
                OnSave();
        }


        private async Task Client_MessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        {
            if (!(arg3 is ITextChannel txt))
                return;
            if (arg2.Author.IsBot || arg2.Author.IsWebhook)
                return;
            var service = Program.Services.GetRequiredService<MsgService>();
            var contents = service.GetContents(arg1.Id);
            var latest = contents.OrderBy(x => x.Timestamp).Where(x => x.Content != arg2.Content).LastOrDefault();
            var latestContent = latest?.Content ?? null;
            if (latestContent == arg2.Content)
                return;
            if (latestContent == null && (DateTime.UtcNow - arg2.CreatedAt.UtcDateTime).TotalSeconds < 1)
                return;
            var builder = new EmbedBuilder()
                .WithTitle("Message Edited")
                .WithColor(Color.Blue)
                .WithDescription(latestContent ?? "[unknown prior content]");
            builder.AddField("Channel", txt.Mention, true);
            builder.AddField("Author", $"{arg2.Author.Id}\r\n<@{arg2.Author.Id}>", true);
            builder.AddField("Link", arg2.GetJumpUrl(), true);
            builder.AddField("Original Sent", SnowflakeUtils.FromSnowflake(arg2.Id).ToString("dd/MM/yy HH:mm:ss.fff"), true);
            await SendLog(txt.Guild, "messages", builder, arg1.Id);
            if (isDirty)
                OnSave();
        }

        #endregion
    }

    class logSave
    {
        public ulong? guildId;
        public Dictionary<ulong, guildSave> guilds;
    }

    class guildSave
    {
        [JsonProperty("c")]
        public ICategoryChannel Category { get; set; }
        [JsonProperty("a")]
        public Dictionary<string, ITextChannel> Actions { get; set; }
        [JsonProperty("r")]
        public IRole Role { get; set; }
    }
}
