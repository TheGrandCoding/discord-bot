using Discord;
using DiscordBot.Classes;
using DiscordBot.Classes.Rules;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class BanAppealsService : SavedService
    {
        Dictionary<ulong, GuildAppeals> Appeals { get; set; } = new Dictionary<ulong, GuildAppeals>();

        public override string GenerateSave()
            => Program.Serialise(Appeals);
        public override void OnLoaded()
        {
            Appeals = Program.Deserialise<Dictionary<ulong, GuildAppeals>>(ReadSave());
        }
        public GuildAppeals GetAllAppeals(IGuild guild)
        {
            if (Appeals.TryGetValue(guild.Id, out var ga))
                return ga;
            ga = new GuildAppeals();
            Appeals[guild.Id] = ga;
            return ga;
        }
        public BanAppeal GetAppeal(IGuild guild, ulong user)
        {
            return GetAllAppeals(guild).Appeals.GetValueOrDefault(user);
        }
        public async Task<BanAppeal> CreateAppeal(IGuild guild, BotUser user)
        {
            var existing = GetAppeal(guild, user.Id);
            if (existing != null)
                return existing;
            var appeals = GetAllAppeals(guild);
            if(appeals.AppealCategory == null)
            {
                appeals.AppealCategory = await guild.CreateCategoryAsync("Ban Appeals", x =>
                {
                    x.PermissionOverwrites = new List<Overwrite>()
                    {
                        new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, Program.NoPerms)
                    };
                });
            }
            var txt = await guild.CreateTextChannelAsync($"appeal-{user.Name}", x =>
            {
                x.Topic = user.Id.ToString();
                x.CategoryId = appeals.AppealCategory.Id;
                x.Position = 0;
            });
            var appeal = new BanAppeal();
            appeal.Appellant = user;
            appeal.Guild = guild;
            appeal.AppealChannel = txt;
            appeals.Appeals[user.Id] = appeal;
            OnSave();
            await txt.SendMessageAsync(embed: new EmbedBuilder()
                .WithAuthor(user.Name, user.GetAvatarUrl())
                .WithTitle($"Ban Appeal")
                .WithDescription($"Original Ban Reason:\r\n> {(appeal.Ban.Reason ?? "No reason provided")}")
                .WithFooter($"{Program.Prefix}appeal mute | {Program.Prefix}appeal approve | {Program.Prefix}appeal reject")
                .WithColor(Color.Red)
                .Build());
            return appeal;
        }
    }

    public class GuildAppeals
    {
        public Dictionary<ulong, BanAppeal> Appeals { get; set; } = new Dictionary<ulong, BanAppeal>();
        public ICategoryChannel AppealCategory { get; set; }
    }
}
