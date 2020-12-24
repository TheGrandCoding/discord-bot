using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Services.Games;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules.Games
{
    [Group("ttt")]
    public class TicTacToe : BotModule
    {
        public TTTService Service { get; set; }

        [Command("setup")]
        [RequireUserPermission(ChannelPermission.ManageChannels)]
        public async Task<RuntimeResult> Setup(int count = 3)
        {
            IRole role = Context.Guild.Roles.FirstOrDefault(x => x.Name == TTTService.RoleName);
            if(role == null)
                role = await Context.Guild.CreateRoleAsync(TTTService.RoleName, isMentionable: false);
            var existing = Context.Guild.VoiceChannels.Count(x => x.Name.StartsWith("ttt-"));
            ICategoryChannel category = Context.Guild.CategoryChannels.FirstOrDefault(x => x.Name == "TTT");
            if(category == null)
            {
                category = await Context.Guild.CreateCategoryChannelAsync("TTT");
                await category.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole, Program.NoPerms);
                await category.AddPermissionOverwriteAsync(role, Program.ReadPerms);
            }
            for (int i = 0; i < (count * count); i++)
            {
                IVoiceChannel vc = Context.Guild.VoiceChannels.FirstOrDefault(x => x.Name.StartsWith($"ttt-{i}"));
                if(vc == null)
                {
                    vc = await Context.Guild.CreateVoiceChannelAsync($"ttt-{i}", x =>
                    {
                        x.CategoryId = category.Id;
                    });
                }
                var invites = await vc.GetInvitesAsync();
                IInvite invite = invites.FirstOrDefault();
                if(invite == null) 
                    invite = await vc.CreateInviteAsync(maxAge: null, maxUses: 1, isTemporary: true);
                await vc.ModifyAsync(x => x.Name = $"ttt-{i}-{invite.Code}");

            }
            return Success("Server has been setup for tic tac toe.");
        }

        [Command("start")]
        public async Task<RuntimeResult> Game(int rows = 3, SocketGuildUser other = null)
        {
            if (Service.Games.Any(x => x.Contains(Context.User)))
                return Error("You are already playing a game.");
            if (Context.Guild.VoiceChannels.Count(x => x.Name.StartsWith("ttt-")) != (rows * rows))
                return Error($"Not enough voice channels, requires {rows * rows}; use `{Program.Prefix}ttt setup {rows}`");
            var game = new TTTGame(Context.Guild, rows);
            var msg = await ReplyAsync(embed: game.ToEmbed());
            var _ = Task.Run(async () =>
            {
                await msg.AddReactionAsync(Emotes.WHITE_CHECK_MARK);
                await msg.AddReactionAsync(Emotes.ARROWS_COUNTERCLOCKWISE);
            });
            var role = Context.Guild.Roles.FirstOrDefault(x => x.Name == TTTService.RoleName);
            var self = Context.User as SocketGuildUser;
            await self.AddRoleAsync(role);
            if (other != null)
                await other.AddRoleAsync(role);
            game.Message = msg;
            Service.Games.Add(game);
            return Success();
        }
    }
}
