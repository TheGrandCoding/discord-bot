using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Name("Role-locked Emojis")]
    [RequireContext(ContextType.Guild)]
    [Group("emoji"), Alias("emote")]
    public class EmojiModule : BotBase
    {
        [Command("list")]
        [Summary("Lists all role-locked emojis")]
        public async Task List()
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "Emojis";
            foreach(var emoji in Context.Guild.Emotes)
            {
                if(emoji.RoleIds != null && emoji.RoleIds.Count > 0)
                {
                    var value = emoji.RoleIds.Select(x => Context.Guild.GetRole(x)?.Mention ?? x.ToString());
                    builder.AddField(emoji.ToString(), string.Join("\r\n", value), true);
                }
            }
            if (builder.Fields.Count == 0)
                builder.WithDescription($"This server has no emojis that are role-locked." + 
                    ((Context.User as SocketGuildUser).GuildPermissions.ManageEmojis ? $"\r\nUse `{Program.Prefix}emoji lock` to lock one" : "")
                    );
            else
                builder.WithDescription($"Below are {builder.Fields.Count} emoji that require one of certain roles to use");
            await ReplyAsync(embed: builder.Build());
        }

        [Command("lock")]
        [Summary("Sets an emote to be useable only by a comma-separated list of roles; ")]
        [RequireUserPermission(GuildPermission.ManageEmojis)]
        public async Task<RuntimeResult> Lock(GuildEmote emote, [Remainder]string roles = "")
        {
            var roleList = new List<IRole>();
            foreach(var x in roles.Split(','))
            {
                var text = x.Trim();
                if (ulong.TryParse(text, out var id))
                    roleList.Add(Context.Guild.GetRole(id));
                else if (MentionUtils.TryParseRole(text, out id))
                    roleList.Add(Context.Guild.GetRole(id));
                else if (!string.IsNullOrWhiteSpace(text))
                    return new BotResult($"Could not parse `{text}` as any role. Either mention it or use the role's id.");
            }
            await Context.Guild.ModifyEmoteAsync(emote, x =>
            {
                x.Roles = roleList;
            });
            await ReplyAsync("Done.");
            return new BotResult();
        }
    }
}
