﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Services.Games;
using DiscordBot.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules.Games
{
    [Group("ttt")]
    [Name("TicTacToe")]
    [RequireContext(ContextType.Guild)]
    public class TicTacToe : BotBase
    {
        public TTTService Service { get; set; }

        [Command("setup")]
        [RequireUserPermission(ChannelPermission.ManageChannels)]
        [Summary("Creates the nessecary channels and roles to play tictactoe of the given board size")]
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
            return await Success ("Server has been setup for tic tac toe.");
        }

        [Command("start")]
        [Summary("Starts a game of tictactoe, of the optional given row size, " +
            "optionally providing the other user who starts as Crosses")]
        public async Task<RuntimeResult> Game(int rows = 3, SocketGuildUser other = null)
        {
            if (Service.Games.Any(x => x.Contains(Context.User)))
                return Error("You are already playing a game.");
            if (Context.Guild.VoiceChannels.Count(x => x.Name.StartsWith("ttt-")) != (rows * rows))
            {
                var existing = Service.Games.Any(x => x.Message.Channel.Id == Context.Channel.Id);
                if(existing)
                    return Error($"There is already a game occuring in this channel.");
                await ReplyAsync("As VCs have not been setup, you must use text messages to indicate where you wish to go. Eg, `A1` as top left");
            }
            var game = new TTTGame(Context.Guild, rows);
            var msg = await ReplyAsync(embed: game.ToEmbed());
            game.Message = msg;
            var _ = Task.Run(game.AddReactions);
            var role = Context.Guild.Roles.FirstOrDefault(x => x.Name == TTTService.RoleName);
            var self = Context.User as SocketGuildUser;
            await self.AddRoleAsync(role);
            if (other != null)
                await other.AddRoleAsync(role);
            Service.Games.Add(game);
            return await Success();
        }
    
        [Command("stop")]
        [Summary("Stops the game happening in the current channel")]
        public async Task<RuntimeResult> Stop()
        {
            var inChannel = Service.Games.FirstOrDefault(x => x.Message.Channel.Id == Context.Channel.Id);
            if (inChannel == null)
                return Error("No game is currently happening in this channel");
            Service.Games.Remove(inChannel);
            await inChannel.Message.DeleteAndTrackAsync("game has ended");
            return await Success("Game removed.");
        }
    }
}
