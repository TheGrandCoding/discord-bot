using Discord;
using Discord.Commands;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireAppealContextAttribute : PreconditionAttribute
    {
        static BanAppealsService _service;
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if(context.Channel is ITextChannel channel)
            {
                _service ??= Program.Services.GetRequiredService<BanAppealsService>();
                if(ulong.TryParse(channel.Topic, out var userId))
                {
                    var appeal = _service.GetAppeal(context.Guild, userId);
                    if(appeal != null)
                        return Task.FromResult(PreconditionResult.FromSuccess());
                }
            }
            return Task.FromResult(PreconditionResult.FromError("Must be executed within an Ban Appeals channel"));
        }
    }
}
