﻿using Discord;
using Interactivity;
using Discord.Commands;
using DiscordBot.Services;
using DiscordBot.Services.BuiltIn;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace DiscordBot.Commands
{
    public abstract class BotBase : ModuleBase<BotCommandContext>
    {
        protected void LogInfo(string message, Exception error = null)
            => Program.LogInfo(message, this.GetType().Name, error);
        protected void LogWarning(string message, Exception error = null)
            => Program.LogWarning(message, this.GetType().Name, error);
        protected void LogDebug(string message, Exception error = null)
            => Program.LogDebug(message, this.GetType().Name, error);
        protected void LogError(Exception error = null, string message = null)
            => Program.LogError(message, this.GetType().Name, error);


        public IServiceProvider Services { get; set; }
        public InteractivityService InteractivityService { get; set; }
        static CmdDisableService cmdDisableService { get; set; }


        /// <summary>
        /// ENSURE THAT YOU CALL base.BeforeExecute! This function handles disabled commands!
        /// </summary>
        protected override void BeforeExecute(CommandInfo command)
        {
            cmdDisableService ??= Services.GetRequiredService<CmdDisableService>();
            if (cmdDisableService.IsDisabled(command, out string reason))
                throw new Exception($"{reason}");
        }

        protected override void AfterExecute(CommandInfo command)
        {
            base.AfterExecute(command);
            Context?.Dispose();
        }

        public async Task<RuntimeResult> Success(string message = null, bool isTTS = false, Embed embed = null)
        {
            if(message != null || embed != null)
            {
                await ReplyAsync("✅ " + message, isTTS, embed);
            }
            return new BotResult();
        }
        public RuntimeResult Error(string message)
            => new BotResult(message);

        public Task<InteractivityResult<object>> PagedReplyAsync(Interactivity.Pagination.PaginatorBuilder builder, TimeSpan? timeout = null)
        {
            return InteractivityService.SendPaginatorAsync(builder.Build(),
                Context.Channel, timeout: timeout);
        }
        public Task<InteractivityResult<SocketMessage>> NextMessageAsync(TimeSpan? timeout = null)
        {
            return InteractivityService.NextMessageAsync(timeout: timeout);
        }

        ButtonComponent getButton(MessageComponent component, string id)
        {
            foreach (var row in component.Components)
            {
                foreach (var cmp in row.Components)
                {
                    if (cmp is ButtonComponent btn)
                        if (btn.CustomId == id)
                            return btn;
                }
            }
            return null;
        }

        public async Task<string> SelectButtonAsync(IEnumerable<string> options, string text = null, Embed embed = null, bool deleteWhenDone = false, TimeSpan? timeout = null)
        {
            var builder = new ComponentBuilder();
            for(int i = 0; i < options.Count(); i++)
            {
                builder.WithButton(ButtonBuilder.CreatePrimaryButton(options.ElementAt(i), $"botbase:{i}"));
            }
            var customId = await SelectButtonAsync(builder, text, embed, deleteWhenDone, timeout).ConfigureAwait(false);
            return options.ElementAt(int.Parse(customId));
        }

        public async Task<bool?> ConfirmAsync(string question = null, Embed embed = null, bool deleteWhenDone = false, TimeSpan? timeout = null, AllowedMentions allowedMentions = null)
        {
            var result = await SelectButtonAsync(new ComponentBuilder()
                .WithButton(ButtonBuilder.CreateDangerButton("Yes", "botbase:true"))
                .WithButton(ButtonBuilder.CreatePrimaryButton("No", "botbase:false")),
                question, embed, deleteWhenDone, timeout, allowedMentions
                );
            if (result == null)
                return null;
            return result == "botbase:true";
        }

        public static Dictionary<ulong, selectHold> selectButton = new Dictionary<ulong, selectHold>();

        public struct selectHold
        {
            public TaskCompletionSource<string> source;
            public ulong id;
        }

        async Task<string> SelectButtonAsync(ComponentBuilder builder, string text = null, Embed embed = null, bool deleteWhenDone = false, TimeSpan? timeout = null,
            AllowedMentions allowedMentions = null)
        {
            if (string.IsNullOrWhiteSpace(text) && embed == null)
                throw new ArgumentNullException($"One of {nameof(text)} or {nameof(embed)} must be non-null");


            var selectionSource = new TaskCompletionSource<string>();

            var selectionTask = selectionSource.Task;
            var timeoutTask = Task.Delay(timeout ?? InteractivityService.DefaultTimeout);

            var components = builder.Build();

            var message = await ReplyAsync(message: text, embed: embed, components: components, allowedMentions: allowedMentions).ConfigureAwait(false);
            selectButton.Add(message.Id, new selectHold()
            {
                source = selectionSource,
                id = Context.User.Id
            });
            var task_result = await Task.WhenAny(timeoutTask, selectionTask).ConfigureAwait(false);

            var result = task_result == selectionTask
                ? await selectionTask.ConfigureAwait(false)
                : null;

            if(deleteWhenDone)
            {
                await message.DeleteAsync().ConfigureAwait(false);
            } else
            { // disable the buttons instead
                var disabledBuilder = new ComponentBuilder();
                int i = 0;
                foreach(var row in builder.ActionRows)
                {
                    foreach(var cmp in row.Components)
                    {
                        if(cmp is ButtonComponent btn)
                        {
                            disabledBuilder.WithButton(btn.Label,
                                btn.CustomId, btn.Style, btn.Emote, btn.Url, true, i);
                        }
                    }
                    i++;
                }
                await message.ModifyAsync(x =>
                {
                    x.Components = disabledBuilder.Build();
                });
            }

            return result;
        }



    }

    public class BotBaseHandler : DiscordBot.Interactions.BotComponentBase
    {
        [Discord.Interactions.ComponentInteraction("botbase:*")]
        public Task Select()
        {
            Program.LogInfo($"Selected for {Context.Interaction.Message.Id} was {Context.Interaction.Data.CustomId}", "BotBaseHandler");
            if (!BotBase.selectButton.TryGetValue(Context.Interaction.Message.Id, out var hold))
                return Task.CompletedTask;
            if (Context.User.Id != hold.id)
                return Task.CompletedTask;
            hold.source.SetResult(Context.Interaction.Data.CustomId);
            return Task.CompletedTask;
        }
    }
}
