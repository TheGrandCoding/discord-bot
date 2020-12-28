using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Discord.Commands;
using DiscordBot.Classes.Chess;
using DiscordBot.MLAPI.Exceptions;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.MLAPI
{
    public class RequireChess : APIPrecondition
    {
        public readonly ChessPerm _perm;
        /// <summary>
        /// If true, will directly equate user's permission and expect an exact match to _perm
        /// <para>
        /// If false, will use bitwise to ensure user's permission hasFlag _perm.
        /// </para>
        /// </summary>
        public bool DirectCompare { get; set; } = false;
        public RequireChess(ChessPerm permission)
        {
            _perm = permission;
        }
        public override bool CanChildOverride(APIPrecondition child)
        {
            return true;
        }

        public override PreconditionResult Check(APIContext context)
        {
            if (context.User == null)
                throw new RedirectException("/login", "You must be logged in");
            using var db = Program.Services.GetRequiredService<ChessDbContext>();
            var player = db.Players.FirstOrDefault(x => x.DiscordAccount == ChessService.cast(context.User.Id));
            if (player == null)
                return PreconditionResult.FromError("Either no connected account, or not found");
            string newline = context.WantsHTML ? "<br/>" : "\n";
            if(DirectCompare)
                return player.Permission == _perm ? PreconditionResult.FromSuccess() : PreconditionResult.FromError($"Permission failure." + newline +
                    $"You have: {player.Permission}" + newline +
                    $"Requires exactly: {_perm}");
            return player.Permission.HasFlag(_perm) ? PreconditionResult.FromSuccess() : PreconditionResult.FromError($"Permission failure." + newline +
                $"You have: {player.Permission}" + newline +
                $"Requires: {_perm}");
        }
    }
}
