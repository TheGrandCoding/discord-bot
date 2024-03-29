﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Classes.Attributes;
using DiscordBot.Services;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("vote")]
    [Name("Democracy")]
    public class DemocracyModule : BotBase
    {
        public DemocracyService Service { get; set; }

        [Command("list")]
        [Summary("Lists all active votes")]
        public async Task List()
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "Democracy in Action";
            builder.Description = $"BE IT ENACTED by the Queen’s most Excellent Majesty, by and with the advice and consent of " +
                $"the Lords Spiritual and Temporal, and Commons, in this present Parliament assembled, " +
                $"and by the authority of the same, as follows:—";
            foreach(var x in Service.Items)
            {
                string value;
                if (x.StatusMessage != null)
                    value = $"[Link]({x.StatusMessage.GetJumpUrl()})";
                else
                    value = $"Message Removed, will be purged when bot next restarts";
                value += $"\r\n";
                value += $"Ayes: {x.Ayes.Count}\r\n" +
                    $"Noes: {x.Noes.Count}\r\n" +
                    $"Not voted: {x.Abstained.Count}";
                builder.AddField(x.getTitle(), value);
            }
            if (builder.Fields.Count == 0)
                builder.AddField("Nothing", "There are no current active votes.");
            await ReplyAsync(embed: builder.Build());
        }


        [Command("question")]
        [Summary("Initiates a vote on a yes/no question")]
        [RequireContext(ContextType.Guild)]
        public async Task Question([Remainder]string question)
        {
            if (question.EndsWith("?"))
                question = question[..^1];
            var msg = await ReplyAsync("[...]");
            var vq = new VoteQuestion(question, Context.Guild, Context.User as SocketGuildUser, msg);
            Service.Register(vq);
            await vq.Update("Initialising...");
        }

        [Command("kick")]
        [Summary("Initiates a vote to kick the provided user")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireContext(ContextType.Guild)]
        public async Task<RuntimeResult> Kick(SocketGuildUser target, [Remainder]string reason)
        {
            var msg = await ReplyAsync("[...]");
            var vk = new VoteKick(Context.Guild, Context.User as SocketGuildUser, target, reason, msg);
            await ReplyAsync("This vote shall be performed when a majority of users having any one of the below roles votes Aye:" +
                "\r\n" + string.Join("\r\n", vk.WhitelistedRoles.Select(x => x.Name)) +
                "\r\n**If this is not desired**, please manually respond with a comma-separated list of roles within 30 seconds",
                allowedMentions: new AllowedMentions(AllowedMentionTypes.None));
            var reply = (await NextMessageAsync(timeout: TimeSpan.FromSeconds(30))).Value;
            if(reply != null && !string.IsNullOrWhiteSpace(reply.Content))
            {
                var split = reply.Content.Split(',');
                vk.WhitelistedRoles = new List<IRole>();
                foreach(var value in split)
                {
                    if(ulong.TryParse(value, out var id) || MentionUtils.TryParseRole(value, out id))
                    {
                        var role = Context.Guild.GetRole(id);
                        vk.WhitelistedRoles.Add(role);
                    } else
                    {
                        var role = Context.Guild.Roles.FirstOrDefault(x => x.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
                        if (role == null)
                            return new BotResult($"Could not parse `{value}` as any role name. Please provide raw ID or @mention");
                        vk.WhitelistedRoles.Add(role);
                    }
                }
                vk.LoadAbstained();
                await vk.Update();
                await ReplyAsync("Using roles:\r\n" + string.Join("\r\n", vk.WhitelistedRoles.Select(x => x.Name)));
            }
            Service.Register(vk);
            await vk.Update("Initialising...");
            return new BotResult();
        }

        [Command("ban")]
        [Summary("Initiates a vote to ban the provided user")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireContext(ContextType.Guild)]
        public async Task<RuntimeResult> Ban(SocketGuildUser target, [Remainder]string reason)
        {
            var msg = await ReplyAsync("[...]");
            var vk = new VoteBan(Context.Guild, Context.User as SocketGuildUser, target, reason, msg);
            await ReplyAsync("This vote shall be performed when a majority of users having any one of the below roles votes Aye:" +
                "\r\n" + string.Join("\r\n", vk.WhitelistedRoles.Select(x => x.Name)) +
                "\r\n**If this is not desired**, please manually respond with a comma-separated list of roles within 30 seconds",
                allowedMentions: new AllowedMentions(AllowedMentionTypes.None));
            var reply = (await NextMessageAsync(timeout: TimeSpan.FromSeconds(30))).Value;
            if (reply != null && !string.IsNullOrWhiteSpace(reply.Content))
            {
                var split = reply.Content.Split(',');
                vk.WhitelistedRoles = new List<IRole>();
                foreach (var value in split)
                {
                    if (ulong.TryParse(value, out var id) || MentionUtils.TryParseRole(value, out id))
                    {
                        var role = Context.Guild.GetRole(id);
                        vk.WhitelistedRoles.Add(role);
                    }
                    else
                    {
                        var role = Context.Guild.Roles.FirstOrDefault(x => x.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
                        if (role == null)
                            return new BotResult($"Could not parse `{value}` as any role name. Please provide raw ID or @mention");
                        vk.WhitelistedRoles.Add(role);
                    }
                }
                vk.LoadAbstained();
                await vk.Update();
                await ReplyAsync("Using roles:\r\n" + string.Join("\r\n", vk.WhitelistedRoles.Select(x => x.Name)));
            }
            Service.Register(vk);
            await vk.Update("Initialising...");
            return new BotResult();
        }

        [Command("remove")]
        [Summary("Removes a given proposal vote")]
        public async Task Remove(ulong id)
        {
            if(!Service.TryGetValue(id, out var item))
            {
                await ReplyAsync("No such proposal exists.");
                return;
            }
            if(Context.User.Id == item.Submitter.Id || (Context.User as IGuildUser).GuildPermissions.Administrator)
            {
                Service.Unregister(item);
                await ReplyAsync("Removed.");
            } else
            {
                await ReplyAsync("Only the person who submitted the proposal may rescind it.");
            }
        }
    
        const ulong roleId = 455776786513657866;
        [Command("membership"), Alias("member")]
        [Summary("Initiates a vote to give or take a role to or from the user")]
        public async Task<RuntimeResult> Membership(SocketGuildUser target, SocketRole role = null)
        {
            role ??= Context.Guild.GetRole(roleId);
            var oper = Context.User as SocketGuildUser;
            if (!oper.Roles.Any(x => x.Id == role.Id))
                return new BotResult("You do not, yourself, have the " + role.Mention + " role");
            var txt = Context.Channel as ITextChannel;
            var everyone = txt.GetPermissionOverwrite(Context.Guild.EveryoneRole);
            if (everyone.GetValueOrDefault().ViewChannel != PermValue.Deny)
                return new BotResult("This channel must be private to " + role.Mention);
            var selected = txt.GetPermissionOverwrite(role);
            if (selected.GetValueOrDefault().ViewChannel != PermValue.Allow)
                return new BotResult($"This channel must be private for " + role.Mention);

            var msg = await ReplyAsync("[...]");
            var vq = new VoteRole(Context.Guild, oper, role, target, !target.Roles.Any(x => x.Id == role.Id), msg);
            Service.Register(vq);
            await vq.Update("Initialising...");
            return new BotResult();
        }
    }

    public class DemocracyService : SavedService
    {
        Dictionary<ulong, VoteItem> VoteItems { get; set; } = new Dictionary<ulong, VoteItem>();

        public override string GenerateSave()
        {
            return Program.Serialise(VoteItems, TypeNameHandling.Auto);
        }

        public override void OnLoaded(IServiceProvider services)
        {
            var loaded = Program.Deserialise<Dictionary<ulong, VoteItem>>(ReadSave());
            foreach(var item in loaded)
            {
                if(item.Value.StatusMessage != null)
                {
                    Register(item.Value);
                    VoteItems[item.Key] = item.Value;
                }
            }
        }

        public bool TryGetValue(ulong id, out VoteItem item) => VoteItems.TryGetValue(id, out item);

        public IReadOnlyCollection<VoteItem> Items => VoteItems.Values.ToImmutableArray();

        public void Register(VoteItem item)
        {
            VoteItems[item.StatusMessage.Id] = item;
            OnSave();
        }

        public void Unregister(VoteItem item)
        {
            VoteItems.Remove(item.StatusMessage.Id);
            OnSave();
        }

        public async Task<bool> ShouldRemove(VoteItem item)
        {
            if(item.NoesHaveIt)
            {
                await item.Update($"**The noes have it, the noes have it!** Unlock!");
                return true;
            } else if (item.AyesHaveIt)
            {
                await item.Update($"**The ayes have it, the ayes have it!** Unlock!");
                var result = await item.PerformAction();
                if(!result.IsSuccess)
                    await item.DiscussionThread.SendMessageAsync($"Unable to perform action for {item.getTitle()}: {result.Reason}");
                return true;
            } else if (item.Deadlocked)
            {
                await item.Update($"Vote has drawn; all votes are counted for and neither suceeds. Status quo - noes have it.");
                return true;
            }
            return false;
        }
    }

    public abstract class VoteItem
    {
        [JsonConstructor]
        protected VoteItem() { }

        public VoteItem(SocketGuild guild, IGuildUser submitter, IUserMessage message)
        {
            Guild = guild;
            Submitter = submitter;
            StatusMessage = message;
        }

        public abstract string Type { get; }
        public SocketGuild Guild { get; set; }
        public IGuildUser Submitter { get; set; }
        public IUserMessage StatusMessage { get; set; }
        public IThreadChannel DiscussionThread { get; set; }
        public List<IRole> WhitelistedRoles { get; set; } = new List<IRole>();
        public List<IGuildUser> Ayes { get; set; } = new List<IGuildUser>();
        public List<IGuildUser> Noes { get; set; } = new List<IGuildUser>();
        public List<IGuildUser> Abstained { get; set; } = new List<IGuildUser>();

        public bool HasVoted(IUser user)
        {
            if (Ayes.Any(x => x.Id == user.Id))
                return true;
            if (Noes.Any(x => x.Id == user.Id))
                return true;
            return false;
        }
        public bool CanVote(IGuildUser user)
        {
            if (user.IsBot)
                return false;
            if (Abstained.Any(x => x.Id == user.Id))
                return true;
            if (WhitelistedRoles.Count == 0)
                return user.IsBot == false; // no whitelist, so any users can
            return user.RoleIds.Any(x => WhitelistedRoles.Any(y => y.Id == x));
        }

        public int Required { get; set; }

        [JsonIgnore]
        public bool AyesHaveIt {  get
            {
                return Ayes.Count >= Required;
            } }
        [JsonIgnore]
        public bool NoesHaveIt
        {
            get
            {
                return Noes.Count >= Required;
            }
        }
        [JsonIgnore]
        public bool Deadlocked {  get
            {
                return AyesHaveIt == false && NoesHaveIt == false && Abstained.Count == 0;
            } }

        [JsonIgnore]
        public bool HasEnded => AyesHaveIt || NoesHaveIt || Deadlocked;


        public abstract string getTitle();
        public abstract string getQuestion();

        public virtual void LoadAbstained()
        {
            var chnl = StatusMessage.Channel as SocketTextChannel;
            Abstained = chnl.Users
                .Where(x => CanVote(x))
                .Cast<IGuildUser>().ToList();
            Required = (int)Math.Floor(Abstained.Count / 2.0) + 1;
        }

        /// <summary>
        /// Removes the user from <see cref="Ayes"/>, <see cref="Noes"/> and <see cref="Abstained"/>; 
        /// returns true, false or null for whether they were in that list, respectively.
        /// </summary>
        public bool? RemoveVotes(IUser user)
        {
            if (Ayes.RemoveAll(x => x.Id == user.Id) > 0)
                return true;
            if (Noes.RemoveAll(x => x.Id == user.Id) > 0)
                return false;
            Abstained.RemoveAll(x => x.Id == user.Id);
            return null;
        }

        public abstract Task<BotResult> PerformAction();

        string collate(IList<IGuildUser> users)
        {
            if (users.Count == 0)
                return "*No votes*";
            return string.Join("\r\n", users.Select(x => x.Nickname ?? x.Username));
        }

        public virtual EmbedBuilder ToEmbed()
        {
            var builder = new EmbedBuilder();
            builder.Title = getTitle();
            builder.Description =
                $"The question is: **{getQuestion()}?**\r\n" +
                $"As many that of of that opinion, do say {Emotes.THUMBS_UP}; of the contray, {Emotes.THUMBS_DOWN}";
            builder.AddField("Ayes", collate(Ayes), true);
            builder.AddField("Abstained", collate(Abstained), true);
            builder.AddField("Noes", collate(Noes), true);
            builder.AddField("Threshold", $"For this measure to pass, it must achieve {Required} ayes");
            builder.WithFooter($"Submitted by {Submitter.Username}");
            return builder;
        }

        public async Task Update(string content = null)
        {
            await StatusMessage.ModifyAsync(x =>
            {
                x.Content = content;
                x.Embeds = new[] { ToEmbed().Build() };
                x.Components = new ComponentBuilder()
                    .WithButton("Aye", "democracy:true", ButtonStyle.Success, Emotes.THUMBS_UP, disabled: HasEnded)
                    .WithButton("No", "democracy:true", ButtonStyle.Primary, Emotes.THUMBS_DOWN, disabled: HasEnded).Build();
            });
            if(DiscussionThread == null)
            {
                DiscussionThread = await (StatusMessage.Channel as SocketTextChannel)
                    .CreateThreadAsync(Program.Clamp(getQuestion(), 100), autoArchiveDuration: ThreadArchiveDuration.OneDay, message: StatusMessage);
                foreach (var usr in Abstained)
                    await DiscussionThread.AddUserAsync(usr);
            }
        }

    }

    public class VoteRole : VoteItem
    {
        [JsonConstructor]
        private VoteRole() { }

        public VoteRole(SocketGuild guild, SocketGuildUser submitter, SocketRole role, SocketGuildUser target, bool adding, IUserMessage msg)
            : base(guild, submitter, msg)
        {
            Target = target;
            Adding = adding;
            Role = role;
            WhitelistedRoles = new List<IRole>() { Role };
            LoadAbstained();
        }

        public override string Type => nameof(VoteRole);

        public bool Adding { get; set; } // whether we are adding the role or removing it
        public IGuildUser Target { get; set; }

        public IRole Role { get; set; }

        public override string getQuestion()
            => $"Should {Target.Mention} {(Adding ? "be given the" : "be stripped of the")} {Role.Mention} role";

        public override string getTitle()
            => $"Vote for Whitelist";

        public async override Task<BotResult> PerformAction()
        {
            if (Adding)
                await Target?.AddRoleAsync(Role, new RequestOptions() { AuditLogReason = "I love democracy" });
            else
                await Target?.RemoveRoleAsync(Role, new RequestOptions() { AuditLogReason = "Voted" });
            using var scope = Program.GlobalServices.CreateScope();
            var db = scope.ServiceProvider.GetBotDb("DemocracyAction");
            var bUser = (await db.GetUserFromDiscord(Target, true)).Value;
            bUser.Verified = Adding;
            bUser.Approved = (bUser.Approved ?? false) || Adding;
            await db.SaveChangesAsync();
            return new BotResult();
        }
    }

    public class VoteQuestion : VoteItem
    {
        [JsonConstructor]
        protected VoteQuestion() { }

        public VoteQuestion(string question, SocketGuild guild, SocketGuildUser submitter, IUserMessage msg)
            : base(guild, submitter, msg)
        {
            Question = question;
            LoadAbstained();
        }

        public override string Type => nameof(VoteQuestion);

        public string Question { get; set; }

        public override string getQuestion()
            => Question;

        public override string getTitle()
            => "Vote on a Question";

        public override Task<BotResult> PerformAction()
        {
            return Task.FromResult(new BotResult());
        }
    }

    public class VoteKick : VoteItem
    {
        [JsonConstructor]
        protected VoteKick() { }
        public VoteKick(SocketGuild guild, SocketGuildUser submitter, SocketGuildUser target, string reason, IUserMessage msg)
            : base(guild, submitter, msg)
        {
            Target = target;
            Reason = reason;
            WhitelistedRoles = guild.Roles
                .Where(x => x.Permissions.KickMembers || x.Permissions.BanMembers || x.Permissions.Administrator)
                .Where(x => x.IsManaged == false && x.Name != "Bot")
                .Cast<IRole>()
                .ToList();
            LoadAbstained();
        }


        public override string Type => nameof(VoteKick);
        public SocketGuildUser Target { get; set; }
        public string Reason { get; set; }

        public override string getTitle() => $"Vote to Kick {Target.Username}#{Target.Discriminator}";
        public override string getQuestion() => $"Should {Target.Nickname ?? Target.Username} temporarily withdraw from this server for '{Reason}'";

        public override async Task<BotResult> PerformAction()
        {
            try
            {
                Target?.SendMessageAsync($"You have been kicked from {(Target?.Guild.Name ?? "the server")}");
                Thread.Sleep(500);
            } catch { }
            try
            {
                await Target?.KickAsync(Reason, new RequestOptions() { AuditLogReason = $"Vote: aye/nay/abs {Ayes.Count}/{Noes.Count}/{Abstained.Count}" });
            } catch (Exception ex)
            {
                Program.LogError(ex, "VoteKick");
                return new BotResult(ex.ToString());
            }
            return new BotResult();
        }
    }

    public class VoteBan : VoteItem
    {
        [JsonConstructor]
        protected VoteBan() { }
        public VoteBan(SocketGuild guild, SocketGuildUser submitter, SocketGuildUser target, string reason, IUserMessage msg)
            : base(guild, submitter, msg)
        {
            Target = target;
            Reason = reason;
            WhitelistedRoles = guild.Roles
                .Where(x => x.Permissions.BanMembers || x.Permissions.Administrator)
                .Where(x => x.IsManaged == false && x.Name != "Bot")
                .Cast<IRole>()
                .ToList();
            LoadAbstained();
        }


        public override string Type => nameof(VoteBan);
        public SocketGuildUser Target { get; set; }
        public string Reason { get; set; }

        public override string getTitle() => $"Vote to Ban {Target.Username}#{Target.Discriminator}";
        public override string getQuestion() => $"Should {Target.Nickname ?? Target.Username} be permanently removed from this server for '{Reason}'";

        public override async Task<BotResult> PerformAction()
        {
            try
            {
                Target?.SendMessageAsync($"You have been banned from {(Target?.Guild.Name ?? "the server")}");
                Thread.Sleep(500);
            }
            catch { }
            try
            {
                await Target?.BanAsync(0, Reason, new RequestOptions() { AuditLogReason = $"Vote: aye/nay/abs {Ayes.Count}/{Noes.Count}/{Abstained.Count}" });
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "VoteBan");
                return new BotResult(ex.ToString());
            }
            return new BotResult();
        }
    }
}
