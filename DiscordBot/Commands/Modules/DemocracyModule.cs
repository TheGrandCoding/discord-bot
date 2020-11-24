using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("vote")]
    [Name("Democracy")]
    public class DemocracyModule : BotModule
    {
        public DemocracyService Service { get; set; }

        [Command("kick")]
        [Summary("Initiates a vote to kick the provided user")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireContext(ContextType.Guild)]
        public async Task<RuntimeResult> Kick(SocketGuildUser target, [Remainder]string reason)
        {
            var msg = await ReplyAsync("[...]");
            Task _ = Task.Run(async () =>
            {
                await msg.AddReactionAsync(Emotes.THUMBS_UP);
                await msg.AddReactionAsync(Emotes.THUMBS_DOWN);
            });
            var vk = new VoteKick(Context.Guild, Context.User as SocketGuildUser, target, reason, msg);
            await ReplyAsync("This vote shall be performed when a majority of users having any one of the below roles votes Aye:" +
                "\r\n" + string.Join("\r\n", vk.WhitelistedRoles.Select(x => x.Name)) +
                "\r\n**If this is not desired**, please manually respond with a comma-separated list of roles within 30 seconds",
                allowedMentions: new AllowedMentions(AllowedMentionTypes.None));
            var reply = await NextMessageAsync(timeout: TimeSpan.FromSeconds(30));
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
            Service.Items[msg.Id] = vk;
            Service.OnSave();
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
            Task _ = Task.Run(async () =>
            {
                await msg.AddReactionAsync(Emotes.THUMBS_UP);
                await msg.AddReactionAsync(Emotes.THUMBS_DOWN);
            });
            var vk = new VoteBan(Context.Guild, Context.User as SocketGuildUser, target, reason, msg);
            await ReplyAsync("This vote shall be performed when a majority of users having any one of the below roles votes Aye:" +
                "\r\n" + string.Join("\r\n", vk.WhitelistedRoles.Select(x => x.Name)) +
                "\r\n**If this is not desired**, please manually respond with a comma-separated list of roles within 30 seconds",
                allowedMentions: new AllowedMentions(AllowedMentionTypes.None));
            var reply = await NextMessageAsync(timeout: TimeSpan.FromSeconds(30));
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
            Service.Items[msg.Id] = vk;
            Service.OnSave();
            await vk.Update("Initialising...");
            return new BotResult();
        }

        [Command("remove")]
        [Summary("Removes a given proposal vote")]
        public async Task Remove(ulong id)
        {
            if(!Service.Items.TryGetValue(id, out var item))
            {
                await ReplyAsync("No such proposal exists.");
                return;
            }
            if(Context.User.Id == item.Submitter.Id || (Context.User as IGuildUser).GuildPermissions.Administrator)
            {
                Service.Items.Remove(id);
                Service.OnSave();
                await ReplyAsync("Removed.");
            } else
            {
                await ReplyAsync("Only the person who submitted the proposal may rescind it.");
            }
        }
    }

    public class DemocracyService : SavedService
    {
        public Dictionary<ulong, VoteItem> Items { get; set; }

        public override string GenerateSave()
        {
            return Program.Serialise(Items, TypeNameHandling.Auto);
        }

        public override void OnLoaded()
        {
            Items = Program.Deserialise<Dictionary<ulong, VoteItem>>(ReadSave());
            Catchup().Wait();
            Program.Client.ReactionAdded += Client_ReactionAdded;
            Program.Client.ReactionRemoved += Client_ReactionRemoved;
        }

        private async Task Client_ReactionRemoved(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (!Items.TryGetValue(arg1.Id, out var item))
                return;
            var user = item.Guild.GetUser(arg3.UserId);
            if (user == null || user.IsBot)
                return;
            var emote = arg3.Emote.Name;
            if (!(emote == Emotes.THUMBS_UP.Name || emote == Emotes.THUMBS_DOWN.Name))
                return;
            if(!item.HasVoted(user))
            {
                await user.SendMessageAsync($"You have not voted for this proposal");
                return;
            }
            if(!item.CanVote(user))
            {
                await user.SendMessageAsync($"You are unable to vote on this proposal");
                return;
            }
            if(emote == Emotes.THUMBS_UP.Name)
            {
                item.Ayes.RemoveAll(x => x.Id == user.Id);
                item.Abstained.Add(user);
            } else if (emote == Emotes.THUMBS_DOWN.Name)
            {
                item.Noes.RemoveAll(x => x.Id == user.Id);
                item.Abstained.Add(user);
            }
            OnSave();
            await item.Update();
        }

        public async Task<bool> ShouldRemove(VoteItem item)
        {
            if(item.NoesHaveIt)
            {
                await item.Update($"**The noes have it, the noes have it**! Unlock!");
                return true;
            } else if (item.AyesHaveIt)
            {
                await item.Update($"**The ayes have it, the ayes have it! Unlock!**");
                var result = await item.PerformAction();
                if(!result.IsSuccess)
                    await item.StatusMessage.Channel.SendMessageAsync($"Unable to perform action for {item.getTitle()}: {result.Reason}");
                return true;
            } else if (item.Deadlocked)
            {
                await item.Update($"Vote has drawn; all votes are counted for and neither suceeds. Status quo - noes have it.");
                return true;
            }
            return false;
        }

        private async System.Threading.Tasks.Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if(Items.TryGetValue(arg1.Id, out var item))
            {
                var user = item.Guild.GetUser(arg3.UserId);
                if (user == null || user.IsBot)
                    return;
                if(item.HasVoted(user))
                {
                    await item.StatusMessage.RemoveReactionAsync(arg3.Emote, user);
                    await user.SendMessageAsync($"You have already voted for this proposal.");
                    return;
                }
                if(!item.CanVote(user))
                {
                    await item.StatusMessage.RemoveReactionAsync(arg3.Emote, user);
                    await user.SendMessageAsync($"You are unable to vote for this proposal.");
                    return;
                }
                if(arg3.Emote.Name == Emotes.THUMBS_UP.Name)
                {
                    item.Ayes.Add(user);
                } else if (arg3.Emote.Name == Emotes.THUMBS_DOWN.Name)
                {
                    item.Noes.Add(user);
                } else
                {
                    await item.StatusMessage.RemoveReactionAsync(arg3.Emote, user);
                    await user.SendMessageAsync($"Reaction must be one of {Emotes.THUMBS_UP} or {Emotes.THUMBS_DOWN}");
                    return;
                }
                item.Abstained.RemoveAll(x => x.Id == user.Id);
                await item.Update();
                var remove = await ShouldRemove(item);
                if (remove)
                {
                    Items.Remove(arg1.Id);
                    await item.StatusMessage.Channel.SendMessageAsync($"The question \"{item.getQuestion()}?\" has been answered; {item.StatusMessage.Content}");
                }
                OnSave();
            }
        }
    
        async Task<List<IGuildUser>> getFor(VoteItem item, IEmote emote)
        {
            var reacted = await item.StatusMessage.GetReactionUsersAsync(emote, 100).FlattenAsync();
            var guildReacted = reacted.Where(x => !x.IsBot).Select(x => item.Guild.GetUser(x.Id));
            return guildReacted.Cast<IGuildUser>().Where(x => item.CanVote(x)).ToList();
        }

        public async Task Catchup()
        {
            var toRemove = new List<ulong>();
            foreach(var keypair in Items)
            {
                var item = keypair.Value;
                if(item.StatusMessage == null)
                {
                    toRemove.Add(keypair.Key);
                    continue;
                }
                item.Ayes = await getFor(item, Emotes.THUMBS_UP);
                item.Noes = await getFor(item, Emotes.THUMBS_DOWN);
                item.Abstained = new List<IGuildUser>();
                item.Abstained = item.Guild.Users.Cast<IGuildUser>().Where(x => x.IsBot == false && item.CanVote(x)).ToList();
                var remove = await ShouldRemove(item);
                if (remove)
                    toRemove.Add(keypair.Key);
            }
            foreach (var id in toRemove)
                Items.Remove(id);
        }
    }

    public abstract class VoteItem
    {
        public abstract string Type { get; }
        public SocketGuild Guild { get; set; }
        public IGuildUser Submitter { get; set; }
        public IUserMessage StatusMessage { get; set; }
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
            if (Abstained.Any(x => x.Id == user.Id))
                return true;
            var can = user.RoleIds.Any(x => WhitelistedRoles.Any(y => y.Id == x));
            if (can)
                Abstained.Add(user);
            return can;
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

        public abstract string getTitle();
        public abstract string getQuestion();

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
            builder.AddField("Noes", collate(Noes), true);
            builder.AddField("Abstained", collate(Abstained), true);
            builder.AddField("Ayes", collate(Ayes), true);
            builder.AddField("Threshold", $"For this measure to pass, it must achieve {Required} ayes");
            builder.WithFooter($"Submitted by {Submitter.Username}");
            return builder;
        }

        public async Task Update(string content = null)
        {
            await StatusMessage.ModifyAsync(x =>
            {
                x.Content = content;
                x.Embed = ToEmbed().Build();
            });
        }

    }

    public class VoteKick : VoteItem
    {
        [JsonConstructor]
        private VoteKick() { }
        public VoteKick(SocketGuild guild, SocketGuildUser submitter, SocketGuildUser target, string reason, IUserMessage msg)
        {
            Guild = guild;
            Submitter = submitter;
            StatusMessage = msg;
            Target = target;
            Reason = reason;
            WhitelistedRoles = guild.Roles
                .Where(x => x.Permissions.KickMembers || x.Permissions.BanMembers || x.Permissions.Administrator)
                .Where(x => x.IsManaged == false && x.Name != "Bot")
                .Cast<IRole>()
                .ToList();
            LoadAbstained();
        }

        public void LoadAbstained()
        {
            Abstained = Guild.Users
                .Where(x => x.IsBot == false && x.Roles.Any(x => WhitelistedRoles.Any(y => x.Id == y.Id)))
                .Cast<IGuildUser>()
                .ToList();
            Required = (int)Math.Floor(Abstained.Count / 2.0) + 1;
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
                Program.LogMsg("Kick", ex);
                return new BotResult(ex.ToString());
            }
            return new BotResult();
        }
    }

    public class VoteBan : VoteItem
    {
        [JsonConstructor]
        private VoteBan() { }
        public VoteBan(SocketGuild guild, SocketGuildUser submitter, SocketGuildUser target, string reason, IUserMessage msg)
        {
            Guild = guild;
            Submitter = submitter;
            StatusMessage = msg;
            Target = target;
            Reason = reason;
            WhitelistedRoles = guild.Roles
                .Where(x => x.Permissions.BanMembers || x.Permissions.Administrator)
                .Where(x => x.IsManaged == false && x.Name != "Bot")
                .Cast<IRole>()
                .ToList();
            LoadAbstained();
        }

        public void LoadAbstained()
        {
            Abstained = Guild.Users
                .Where(x => x.IsBot == false && x.Roles.Any(x => WhitelistedRoles.Any(y => x.Id == y.Id)))
                .Cast<IGuildUser>()
                .ToList();
            Required = (int)Math.Floor(Abstained.Count / 2.0) + 1;
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
                Program.LogMsg("Ban", ex);
                return new BotResult(ex.ToString());
            }
            return new BotResult();
        }
    }
}
