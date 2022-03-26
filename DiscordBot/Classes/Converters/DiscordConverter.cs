using Discord;
using Discord.Audio;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Classes
{
    public class DiscordConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ITextChannel)
                || objectType == typeof(SocketTextChannel)
                || objectType == typeof(RestTextChannel)
                || objectType == typeof(IThreadChannel)
                || objectType == typeof(SocketThreadChannel)
                || objectType == typeof(RestThreadChannel)
                || objectType == typeof(IVoiceChannel)
                || objectType == typeof(SocketVoiceChannel)
                || objectType == typeof(RestVoiceChannel)
                || objectType == typeof(SocketGuildUser)
                || objectType == typeof(IMessage)
                || objectType == typeof(SocketMessage)
                || objectType == typeof(RestMessage)
                || objectType == typeof(ISystemMessage)
                || objectType == typeof(SocketSystemMessage)
                || objectType == typeof(RestSystemMessage)
                || objectType == typeof(IUserMessage)
                || objectType == typeof(RestUserMessage)
                || objectType == typeof(SocketUserMessage)
                || objectType == typeof(ICategoryChannel)
                || objectType == typeof(SocketCategoryChannel)
                || objectType == typeof(RestCategoryChannel)
                || objectType == typeof(IRole)
                || objectType == typeof(SocketRole)
                || objectType == typeof(RestRole)
                || objectType == typeof(IGuild)
                || objectType == typeof(SocketGuild)
                || objectType == typeof(IGuildUser)
                || objectType == typeof(SocketGuildUser)
                || objectType == typeof(SocketUser)
                || objectType == typeof(IUser);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value == null)
                return null;
            string thing = (string)reader.Value;
            var split = thing.Split('.');
            var gid = ulong.Parse(split[0]);
            var id = ulong.Parse(split[1]);

            var guild = Program.Client.GetGuild(gid);
            var lazyGuild = new LazyGuild(guild);
            if (objectType == typeof(SocketGuild) || objectType == typeof(IGuild))
                return guild;
            if (objectType == typeof(SocketGuildUser) || objectType == typeof(IGuildUser))
                return new LazyGuildUser(lazyGuild, id);
            if (objectType == typeof(SocketUser) || objectType == typeof(IUser))
                return guild != null ? guild.GetUser(id) : Program.Client.GetUser(id);
            if (objectType == typeof(SocketThreadChannel) || objectType == typeof(IThreadChannel))
                return Program.Client.GetChannel(id) as SocketThreadChannel;
            if (objectType == typeof(SocketTextChannel) || objectType == typeof(ITextChannel))
                return new LazyGuildTextChannel(lazyGuild, id);
            if (objectType == typeof(SocketCategoryChannel) || objectType == typeof(ICategoryChannel))
                return guild.GetCategoryChannel(id);
            if (objectType == typeof(SocketVoiceChannel) || objectType == typeof(IVoiceChannel))
                return guild.GetVoiceChannel(id);
            if (objectType == typeof(SocketGuildUser) || objectType == typeof(IGuildUser))
                return guild.GetUser(id);
            if (objectType == typeof(SocketRole) || objectType == typeof(IRole))
                return guild.GetRole(id);
            if (objectType == typeof(SocketUserMessage) || objectType == typeof(IUserMessage)
                || objectType == typeof(SocketSystemMessage) || objectType == typeof(ISystemMessage)
                || objectType == typeof(SocketMessage) || objectType == typeof(IMessage))
            {
                var msgId = ulong.Parse(split[2]);
                return new LazyIUserMessage(gid, id, msgId);
            }
            return null;
        }

        public string GetValue(object value)
        {
            if (value is IThreadChannel th)
            {
                return $"{th.GuildId}.{th.Id}";
            } else if (value is IGuildChannel gc)
            {
                return $"{gc.GuildId}.{gc.Id}";
            }
            else if (value is IRole rl)
            {
                return $"{rl.Guild.Id}.{rl.Id}";
            }
            else if (value is IGuildUser gu)
            {
                return $"{gu.GuildId}.{gu.Id}";
            } else if(value is LazyIUserMessage lazy)
            {
                return lazy.ToString();
            } else if (value is IMessage s)
            {
                ulong gId = 0;
                ulong cId = s.Channel.Id;
                ulong mId = s.Id;
                if (s.Channel is IGuildChannel c)
                    gId = c.GuildId;
                if (s.Channel is IDMChannel d)
                    cId = d.Recipient.Id;
                return $"{gId}.{cId}.{mId}";
            } else if (value is IGuild g)
            {
                return $"{g.Id}.0";
            }
            else if (value is IEntity<ulong> eu)
            {
                return $"0.{eu.Id}";
            }
            return (value ?? "<null>").ToString();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var jval = new JValue(GetValue(value));
            jval.WriteTo(writer);
        }
    }

    internal class LazyGuild : IGuild
    {
        public LazyGuild(SocketGuild guild)
        {
            _guild = guild;
            _id = guild.Id;
        }
        private SocketGuild _guild;
        private ulong _id;
        public SocketGuild guild { get
            {
                if (_guild == null)
                    _guild = Program.Client.GetGuild(_id);
                return _guild;
            } }

        public ulong Id => _id;
        public SocketGuildUser GetUser(ulong id) => guild.GetUser(id);
        public SocketTextChannel GetTextChannel(ulong id) => guild.GetTextChannel(id);

        #region GuildItems
        public string Name => ((IGuild)guild).Name;
        public int AFKTimeout => ((IGuild)guild).AFKTimeout;
        public bool IsWidgetEnabled => ((IGuild)guild).IsWidgetEnabled;
        public DefaultMessageNotifications DefaultMessageNotifications => ((IGuild)guild).DefaultMessageNotifications;
        public MfaLevel MfaLevel => ((IGuild)guild).MfaLevel;
        public VerificationLevel VerificationLevel => ((IGuild)guild).VerificationLevel;
        public ExplicitContentFilterLevel ExplicitContentFilter => ((IGuild)guild).ExplicitContentFilter;
        public string IconId => ((IGuild)guild).IconId;
        public string IconUrl => ((IGuild)guild).IconUrl;
        public string SplashId => ((IGuild)guild).SplashId;
        public string SplashUrl => ((IGuild)guild).SplashUrl;
        public string DiscoverySplashId => ((IGuild)guild).DiscoverySplashId;
        public string DiscoverySplashUrl => ((IGuild)guild).DiscoverySplashUrl;
        public bool Available => ((IGuild)guild).Available;
        public ulong? AFKChannelId => ((IGuild)guild).AFKChannelId;
        public ulong? WidgetChannelId => ((IGuild)guild).WidgetChannelId;
        public ulong? SystemChannelId => ((IGuild)guild).SystemChannelId;
        public ulong? RulesChannelId => ((IGuild)guild).RulesChannelId;
        public ulong? PublicUpdatesChannelId => ((IGuild)guild).PublicUpdatesChannelId;
        public ulong OwnerId => ((IGuild)guild).OwnerId;
        public ulong? ApplicationId => ((IGuild)guild).ApplicationId;
        public string VoiceRegionId => ((IGuild)guild).VoiceRegionId;
        public IAudioClient AudioClient => ((IGuild)guild).AudioClient;
        public IRole EveryoneRole => ((IGuild)guild).EveryoneRole;
        public IReadOnlyCollection<GuildEmote> Emotes => ((IGuild)guild).Emotes;
        public IReadOnlyCollection<ICustomSticker> Stickers => ((IGuild)guild).Stickers;
        public GuildFeatures Features => ((IGuild)guild).Features;
        public IReadOnlyCollection<IRole> Roles => ((IGuild)guild).Roles;
        public PremiumTier PremiumTier => ((IGuild)guild).PremiumTier;
        public string BannerId => ((IGuild)guild).BannerId;
        public string BannerUrl => ((IGuild)guild).BannerUrl;
        public string VanityURLCode => ((IGuild)guild).VanityURLCode;
        public SystemChannelMessageDeny SystemChannelFlags => ((IGuild)guild).SystemChannelFlags;
        public string Description => ((IGuild)guild).Description;
        public int PremiumSubscriptionCount => ((IGuild)guild).PremiumSubscriptionCount;
        public int? MaxPresences => ((IGuild)guild).MaxPresences;
        public int? MaxMembers => ((IGuild)guild).MaxMembers;
        public int? MaxVideoChannelUsers => ((IGuild)guild).MaxVideoChannelUsers;
        public int? ApproximateMemberCount => ((IGuild)guild).ApproximateMemberCount;
        public int? ApproximatePresenceCount => ((IGuild)guild).ApproximatePresenceCount;

        public int MaxBitrate => ((IGuild)guild).MaxBitrate;

        public string PreferredLocale => ((IGuild)guild).PreferredLocale;

        public NsfwLevel NsfwLevel => ((IGuild)guild).NsfwLevel;

        public CultureInfo PreferredCulture => ((IGuild)guild).PreferredCulture;

        public bool IsBoostProgressBarEnabled => ((IGuild)guild).IsBoostProgressBarEnabled;

        public ulong MaxUploadLimit => ((IGuild)guild).MaxUploadLimit;

        public DateTimeOffset CreatedAt => ((ISnowflakeEntity)guild).CreatedAt;


        public Task AddBanAsync(IUser user, int pruneDays = 0, string reason = null, RequestOptions options = null)
        {
            return ((IGuild)guild).AddBanAsync(user, pruneDays, reason, options);
        }

        public Task AddBanAsync(ulong userId, int pruneDays = 0, string reason = null, RequestOptions options = null)
        {
            return ((IGuild)guild).AddBanAsync(userId, pruneDays, reason, options);
        }

        public Task<IGuildUser> AddGuildUserAsync(ulong userId, string accessToken, Action<AddGuildUserProperties> func = null, RequestOptions options = null)
        {
            return ((IGuild)guild).AddGuildUserAsync(userId, accessToken, func, options);
        }

        public Task<IReadOnlyCollection<IApplicationCommand>> BulkOverwriteApplicationCommandsAsync(ApplicationCommandProperties[] properties, RequestOptions options = null)
        {
            return ((IGuild)guild).BulkOverwriteApplicationCommandsAsync(properties, options);
        }

        public Task<IApplicationCommand> CreateApplicationCommandAsync(ApplicationCommandProperties properties, RequestOptions options = null)
        {
            return ((IGuild)guild).CreateApplicationCommandAsync(properties, options);
        }

        public Task<ICategoryChannel> CreateCategoryAsync(string name, Action<GuildChannelProperties> func = null, RequestOptions options = null)
        {
            return ((IGuild)guild).CreateCategoryAsync(name, func, options);
        }

        public Task<GuildEmote> CreateEmoteAsync(string name, Image image, Optional<IEnumerable<IRole>> roles = default, RequestOptions options = null)
        {
            return ((IGuild)guild).CreateEmoteAsync(name, image, roles, options);
        }

        public Task<IGuildScheduledEvent> CreateEventAsync(string name, DateTimeOffset startTime, GuildScheduledEventType type, GuildScheduledEventPrivacyLevel privacyLevel = GuildScheduledEventPrivacyLevel.Private, string description = null, DateTimeOffset? endTime = null, ulong? channelId = null, string location = null, RequestOptions options = null)
        {
            return ((IGuild)guild).CreateEventAsync(name, startTime, type, privacyLevel, description, endTime, channelId, location, options);
        }

        public Task<IGuildIntegration> CreateIntegrationAsync(ulong id, string type, RequestOptions options = null)
        {
            return ((IGuild)guild).CreateIntegrationAsync(id, type, options);
        }

        public Task<IRole> CreateRoleAsync(string name, GuildPermissions? permissions = null, Color? color = null, bool isHoisted = false, RequestOptions options = null)
        {
            return ((IGuild)guild).CreateRoleAsync(name, permissions, color, isHoisted, options);
        }

        public Task<IRole> CreateRoleAsync(string name, GuildPermissions? permissions = null, Color? color = null, bool isHoisted = false, bool isMentionable = false, RequestOptions options = null)
        {
            return ((IGuild)guild).CreateRoleAsync(name, permissions, color, isHoisted, isMentionable, options);
        }

        public Task<IStageChannel> CreateStageChannelAsync(string name, Action<VoiceChannelProperties> func = null, RequestOptions options = null)
        {
            return ((IGuild)guild).CreateStageChannelAsync(name, func, options);
        }

        public Task<ICustomSticker> CreateStickerAsync(string name, string description, IEnumerable<string> tags, Image image, RequestOptions options = null)
        {
            return ((IGuild)guild).CreateStickerAsync(name, description, tags, image, options);
        }

        public Task<ICustomSticker> CreateStickerAsync(string name, string description, IEnumerable<string> tags, string path, RequestOptions options = null)
        {
            return ((IGuild)guild).CreateStickerAsync(name, description, tags, path, options);
        }

        public Task<ICustomSticker> CreateStickerAsync(string name, string description, IEnumerable<string> tags, Stream stream, string filename, RequestOptions options = null)
        {
            return ((IGuild)guild).CreateStickerAsync(name, description, tags, stream, filename, options);
        }

        public Task<ITextChannel> CreateTextChannelAsync(string name, Action<TextChannelProperties> func = null, RequestOptions options = null)
        {
            return ((IGuild)guild).CreateTextChannelAsync(name, func, options);
        }

        public Task<IVoiceChannel> CreateVoiceChannelAsync(string name, Action<VoiceChannelProperties> func = null, RequestOptions options = null)
        {
            return ((IGuild)guild).CreateVoiceChannelAsync(name, func, options);
        }

        public Task DeleteAsync(RequestOptions options = null)
        {
            return ((IDeletable)guild).DeleteAsync(options);
        }

        public Task DeleteEmoteAsync(GuildEmote emote, RequestOptions options = null)
        {
            return ((IGuild)guild).DeleteEmoteAsync(emote, options);
        }

        public Task DeleteStickerAsync(ICustomSticker sticker, RequestOptions options = null)
        {
            return ((IGuild)guild).DeleteStickerAsync(sticker, options);
        }

        public Task DisconnectAsync(IGuildUser user)
        {
            return ((IGuild)guild).DisconnectAsync(user);
        }

        public Task DownloadUsersAsync()
        {
            return ((IGuild)guild).DownloadUsersAsync();
        }

        public Task<IVoiceChannel> GetAFKChannelAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetAFKChannelAsync(mode, options);
        }

        public Task<IApplicationCommand> GetApplicationCommandAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetApplicationCommandAsync(id, mode, options);
        }

        public Task<IReadOnlyCollection<IApplicationCommand>> GetApplicationCommandsAsync(RequestOptions options = null)
        {
            return ((IGuild)guild).GetApplicationCommandsAsync(options);
        }

        public Task<IReadOnlyCollection<IAuditLogEntry>> GetAuditLogsAsync(int limit = 100, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null, ulong? beforeId = null, ulong? userId = null, ActionType? actionType = null)
        {
            return ((IGuild)guild).GetAuditLogsAsync(limit, mode, options, beforeId, userId, actionType);
        }

        public Task<IBan> GetBanAsync(IUser user, RequestOptions options = null)
        {
            return ((IGuild)guild).GetBanAsync(user, options);
        }

        public Task<IBan> GetBanAsync(ulong userId, RequestOptions options = null)
        {
            return ((IGuild)guild).GetBanAsync(userId, options);
        }

        public Task<IReadOnlyCollection<IBan>> GetBansAsync(RequestOptions options = null)
        {
            return ((IGuild)guild).GetBansAsync(options);
        }

        public Task<IReadOnlyCollection<ICategoryChannel>> GetCategoriesAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetCategoriesAsync(mode, options);
        }

        public Task<IGuildChannel> GetChannelAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetChannelAsync(id, mode, options);
        }

        public Task<IReadOnlyCollection<IGuildChannel>> GetChannelsAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetChannelsAsync(mode, options);
        }

        public Task<IGuildUser> GetCurrentUserAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetCurrentUserAsync(mode, options);
        }

        public Task<ITextChannel> GetDefaultChannelAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetDefaultChannelAsync(mode, options);
        }

        public Task<GuildEmote> GetEmoteAsync(ulong id, RequestOptions options = null)
        {
            return ((IGuild)guild).GetEmoteAsync(id, options);
        }

        public Task<IReadOnlyCollection<GuildEmote>> GetEmotesAsync(RequestOptions options = null)
        {
            return ((IGuild)guild).GetEmotesAsync(options);
        }

        public Task<IGuildScheduledEvent> GetEventAsync(ulong id, RequestOptions options = null)
        {
            return ((IGuild)guild).GetEventAsync(id, options);
        }

        public Task<IReadOnlyCollection<IGuildScheduledEvent>> GetEventsAsync(RequestOptions options = null)
        {
            return ((IGuild)guild).GetEventsAsync(options);
        }

        public Task<IReadOnlyCollection<IGuildIntegration>> GetIntegrationsAsync(RequestOptions options = null)
        {
            return ((IGuild)guild).GetIntegrationsAsync(options);
        }

        public Task<IReadOnlyCollection<IInviteMetadata>> GetInvitesAsync(RequestOptions options = null)
        {
            return ((IGuild)guild).GetInvitesAsync(options);
        }

        public Task<IGuildUser> GetOwnerAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetOwnerAsync(mode, options);
        }

        public Task<ITextChannel> GetPublicUpdatesChannelAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetPublicUpdatesChannelAsync(mode, options);
        }

        public IRole GetRole(ulong id)
        {
            return ((IGuild)guild).GetRole(id);
        }

        public Task<ITextChannel> GetRulesChannelAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetRulesChannelAsync(mode, options);
        }

        public Task<IStageChannel> GetStageChannelAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetStageChannelAsync(id, mode, options);
        }

        public Task<IReadOnlyCollection<IStageChannel>> GetStageChannelsAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetStageChannelsAsync(mode, options);
        }

        public Task<ICustomSticker> GetStickerAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetStickerAsync(id, mode, options);
        }

        public Task<IReadOnlyCollection<ICustomSticker>> GetStickersAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetStickersAsync(mode, options);
        }

        public Task<ITextChannel> GetSystemChannelAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetSystemChannelAsync(mode, options);
        }

        public Task<ITextChannel> GetTextChannelAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetTextChannelAsync(id, mode, options);
        }

        public Task<IReadOnlyCollection<ITextChannel>> GetTextChannelsAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetTextChannelsAsync(mode, options);
        }

        public Task<IThreadChannel> GetThreadChannelAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetThreadChannelAsync(id, mode, options);
        }

        public Task<IReadOnlyCollection<IThreadChannel>> GetThreadChannelsAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetThreadChannelsAsync(mode, options);
        }

        public Task<IGuildUser> GetUserAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetUserAsync(id, mode, options);
        }

        public Task<IReadOnlyCollection<IGuildUser>> GetUsersAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetUsersAsync(mode, options);
        }

        public Task<IInviteMetadata> GetVanityInviteAsync(RequestOptions options = null)
        {
            return ((IGuild)guild).GetVanityInviteAsync(options);
        }

        public Task<IVoiceChannel> GetVoiceChannelAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetVoiceChannelAsync(id, mode, options);
        }

        public Task<IReadOnlyCollection<IVoiceChannel>> GetVoiceChannelsAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetVoiceChannelsAsync(mode, options);
        }

        public Task<IReadOnlyCollection<IVoiceRegion>> GetVoiceRegionsAsync(RequestOptions options = null)
        {
            return ((IGuild)guild).GetVoiceRegionsAsync(options);
        }

        public Task<IWebhook> GetWebhookAsync(ulong id, RequestOptions options = null)
        {
            return ((IGuild)guild).GetWebhookAsync(id, options);
        }

        public Task<IReadOnlyCollection<IWebhook>> GetWebhooksAsync(RequestOptions options = null)
        {
            return ((IGuild)guild).GetWebhooksAsync(options);
        }

        public Task<IGuildChannel> GetWidgetChannelAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).GetWidgetChannelAsync(mode, options);
        }

        public Task LeaveAsync(RequestOptions options = null)
        {
            return ((IGuild)guild).LeaveAsync(options);
        }

        public Task ModifyAsync(Action<GuildProperties> func, RequestOptions options = null)
        {
            return ((IGuild)guild).ModifyAsync(func, options);
        }

        public Task<GuildEmote> ModifyEmoteAsync(GuildEmote emote, Action<EmoteProperties> func, RequestOptions options = null)
        {
            return ((IGuild)guild).ModifyEmoteAsync(emote, func, options);
        }

        public Task ModifyWidgetAsync(Action<GuildWidgetProperties> func, RequestOptions options = null)
        {
            return ((IGuild)guild).ModifyWidgetAsync(func, options);
        }

        public Task MoveAsync(IGuildUser user, IVoiceChannel targetChannel)
        {
            return ((IGuild)guild).MoveAsync(user, targetChannel);
        }

        public Task<int> PruneUsersAsync(int days = 30, bool simulate = false, RequestOptions options = null, IEnumerable<ulong> includeRoleIds = null)
        {
            return ((IGuild)guild).PruneUsersAsync(days, simulate, options, includeRoleIds);
        }

        public Task RemoveBanAsync(IUser user, RequestOptions options = null)
        {
            return ((IGuild)guild).RemoveBanAsync(user, options);
        }

        public Task RemoveBanAsync(ulong userId, RequestOptions options = null)
        {
            return ((IGuild)guild).RemoveBanAsync(userId, options);
        }

        public Task ReorderChannelsAsync(IEnumerable<ReorderChannelProperties> args, RequestOptions options = null)
        {
            return ((IGuild)guild).ReorderChannelsAsync(args, options);
        }

        public Task ReorderRolesAsync(IEnumerable<ReorderRoleProperties> args, RequestOptions options = null)
        {
            return ((IGuild)guild).ReorderRolesAsync(args, options);
        }

        public Task<IReadOnlyCollection<IGuildUser>> SearchUsersAsync(string query, int limit = 1000, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuild)guild).SearchUsersAsync(query, limit, mode, options);
        }

        #endregion // Guild Items
    }

    internal class LazyObject
    {
        public LazyObject(ulong id)
        {
            _id = id;
        }
        private ulong _id;
        public ulong Id => _id;
    }

    internal class LazyGuildObject : LazyObject
    {
        public LazyGuildObject(LazyGuild _guild, ulong id) : base(id)
        {
            guild = _guild;
        }
        protected LazyGuild guild;
    }

    internal class LazyGuildUser : LazyGuildObject, IGuildUser
    {
        public LazyGuildUser(LazyGuild _guild, ulong userId) : base(_guild, userId)
        {
        }

        private SocketGuildUser _user;
        public SocketGuildUser User { get
            {
                if (_user == null)
                    _user = guild.GetUser(Id);
                return _user;
            } }

        #region GuildUser items
        public DateTimeOffset? JoinedAt => ((IGuildUser)User).JoinedAt;

        public string Nickname => ((IGuildUser)User).Nickname;

        public string GuildAvatarId => ((IGuildUser)User).GuildAvatarId;

        public GuildPermissions GuildPermissions => ((IGuildUser)User).GuildPermissions;

        public IGuild Guild => ((IGuildUser)User).Guild;

        public ulong GuildId => ((IGuildUser)User).GuildId;

        public DateTimeOffset? PremiumSince => ((IGuildUser)User).PremiumSince;

        public IReadOnlyCollection<ulong> RoleIds => ((IGuildUser)User).RoleIds;

        public bool? IsPending => ((IGuildUser)User).IsPending;

        public int Hierarchy => ((IGuildUser)User).Hierarchy;

        public DateTimeOffset? TimedOutUntil => ((IGuildUser)User).TimedOutUntil;

        public string AvatarId => ((IUser)User).AvatarId;

        public string Discriminator => ((IUser)User).Discriminator;

        public ushort DiscriminatorValue => ((IUser)User).DiscriminatorValue;

        public bool IsBot => ((IUser)User).IsBot;

        public bool IsWebhook => ((IUser)User).IsWebhook;

        public string Username => ((IUser)User).Username;

        public UserProperties? PublicFlags => ((IUser)User).PublicFlags;

        public DateTimeOffset CreatedAt => ((ISnowflakeEntity)User).CreatedAt;

        public string Mention => ((IMentionable)User).Mention;

        public UserStatus Status => ((IPresence)User).Status;

        public IReadOnlyCollection<ClientType> ActiveClients => ((IPresence)User).ActiveClients;

        public IReadOnlyCollection<IActivity> Activities => ((IPresence)User).Activities;

        public bool IsDeafened => ((IVoiceState)User).IsDeafened;

        public bool IsMuted => ((IVoiceState)User).IsMuted;

        public bool IsSelfDeafened => ((IVoiceState)User).IsSelfDeafened;

        public bool IsSelfMuted => ((IVoiceState)User).IsSelfMuted;

        public bool IsSuppressed => ((IVoiceState)User).IsSuppressed;

        public IVoiceChannel VoiceChannel => ((IVoiceState)User).VoiceChannel;

        public string VoiceSessionId => ((IVoiceState)User).VoiceSessionId;

        public bool IsStreaming => ((IVoiceState)User).IsStreaming;

        public DateTimeOffset? RequestToSpeakTimestamp => ((IVoiceState)User).RequestToSpeakTimestamp;

        public ChannelPermissions GetPermissions(IGuildChannel channel)
        {
            return ((IGuildUser)User).GetPermissions(channel);
        }

        public string GetGuildAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
        {
            return ((IGuildUser)User).GetGuildAvatarUrl(format, size);
        }

        public Task KickAsync(string reason = null, RequestOptions options = null)
        {
            return ((IGuildUser)User).KickAsync(reason, options);
        }

        public Task ModifyAsync(Action<GuildUserProperties> func, RequestOptions options = null)
        {
            return ((IGuildUser)User).ModifyAsync(func, options);
        }

        public Task AddRoleAsync(ulong roleId, RequestOptions options = null)
        {
            return ((IGuildUser)User).AddRoleAsync(roleId, options);
        }

        public Task AddRoleAsync(IRole role, RequestOptions options = null)
        {
            return ((IGuildUser)User).AddRoleAsync(role, options);
        }

        public Task AddRolesAsync(IEnumerable<ulong> roleIds, RequestOptions options = null)
        {
            return ((IGuildUser)User).AddRolesAsync(roleIds, options);
        }

        public Task AddRolesAsync(IEnumerable<IRole> roles, RequestOptions options = null)
        {
            return ((IGuildUser)User).AddRolesAsync(roles, options);
        }

        public Task RemoveRoleAsync(ulong roleId, RequestOptions options = null)
        {
            return ((IGuildUser)User).RemoveRoleAsync(roleId, options);
        }

        public Task RemoveRoleAsync(IRole role, RequestOptions options = null)
        {
            return ((IGuildUser)User).RemoveRoleAsync(role, options);
        }

        public Task RemoveRolesAsync(IEnumerable<ulong> roleIds, RequestOptions options = null)
        {
            return ((IGuildUser)User).RemoveRolesAsync(roleIds, options);
        }

        public Task RemoveRolesAsync(IEnumerable<IRole> roles, RequestOptions options = null)
        {
            return ((IGuildUser)User).RemoveRolesAsync(roles, options);
        }

        public Task SetTimeOutAsync(TimeSpan span, RequestOptions options = null)
        {
            return ((IGuildUser)User).SetTimeOutAsync(span, options);
        }

        public Task RemoveTimeOutAsync(RequestOptions options = null)
        {
            return ((IGuildUser)User).RemoveTimeOutAsync(options);
        }

        public string GetAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
        {
            return ((IUser)User).GetAvatarUrl(format, size);
        }

        public string GetDefaultAvatarUrl()
        {
            return ((IUser)User).GetDefaultAvatarUrl();
        }

        public Task<IDMChannel> CreateDMChannelAsync(RequestOptions options = null)
        {
            return ((IUser)User).CreateDMChannelAsync(options);
        }

        #endregion

    }

    internal class LazyGuildTextChannel : LazyGuildObject, ITextChannel
    {
        public LazyGuildTextChannel(LazyGuild _guild, ulong channelId) : base(_guild, channelId)
        {
            Program.LogVerbose($"Lazily making text channel {_guild.Id}:{channelId}", "LazyGuildTChannel");
        }

        private SocketTextChannel _channel;
        public SocketTextChannel Channel { get
            {
                if (_channel == null)
                {
                    Program.LogVerbose($"Loading text channel {guild.Id}:{Id}", "LazyGuildTChannel");
                    _channel = guild.GetTextChannel(Id);
                }
                return _channel;
            } }

        #region GuildTextChannel items

        public bool IsNsfw => ((ITextChannel)Channel).IsNsfw;

        public string Topic => ((ITextChannel)Channel).Topic;

        public int SlowModeInterval => ((ITextChannel)Channel).SlowModeInterval;

        public string Mention => ((IMentionable)Channel).Mention;

        public ulong? CategoryId => ((INestedChannel)Channel).CategoryId;

        public int Position => ((IGuildChannel)Channel).Position;

        public IGuild Guild => ((IGuildChannel)Channel).Guild;

        public ulong GuildId => ((IGuildChannel)Channel).GuildId;

        public IReadOnlyCollection<Overwrite> PermissionOverwrites => ((IGuildChannel)Channel).PermissionOverwrites;

        public string Name => ((IChannel)Channel).Name;

        public DateTimeOffset CreatedAt => ((ISnowflakeEntity)Channel).CreatedAt;

        public Task DeleteMessagesAsync(IEnumerable<IMessage> messages, RequestOptions options = null)
        {
            return ((ITextChannel)Channel).DeleteMessagesAsync(messages, options);
        }

        public Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, RequestOptions options = null)
        {
            return ((ITextChannel)Channel).DeleteMessagesAsync(messageIds, options);
        }

        public Task ModifyAsync(Action<TextChannelProperties> func, RequestOptions options = null)
        {
            return ((ITextChannel)Channel).ModifyAsync(func, options);
        }

        public Task<IWebhook> CreateWebhookAsync(string name, Stream avatar = null, RequestOptions options = null)
        {
            return ((ITextChannel)Channel).CreateWebhookAsync(name, avatar, options);
        }

        public Task<IWebhook> GetWebhookAsync(ulong id, RequestOptions options = null)
        {
            return ((ITextChannel)Channel).GetWebhookAsync(id, options);
        }

        public Task<IReadOnlyCollection<IWebhook>> GetWebhooksAsync(RequestOptions options = null)
        {
            return ((ITextChannel)Channel).GetWebhooksAsync(options);
        }

        public Task<IThreadChannel> CreateThreadAsync(string name, ThreadType type = ThreadType.PublicThread, ThreadArchiveDuration autoArchiveDuration = ThreadArchiveDuration.OneDay, IMessage message = null, bool? invitable = null, int? slowmode = null, RequestOptions options = null)
        {
            return ((ITextChannel)Channel).CreateThreadAsync(name, type, autoArchiveDuration, message, invitable, slowmode, options);
        }

        public Task<IReadOnlyCollection<IThreadChannel>> GetPublicArchivedThreadsAsync(DateTimeOffset? before = null, int? limit = null, RequestOptions options = null)
        {
            return ((ITextChannel)Channel).GetPublicArchivedThreadsAsync(before, limit, options);
        }

        public Task<IUserMessage> SendMessageAsync(string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null)
        {
            return ((IMessageChannel)Channel).SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference, components, stickers, embeds);
        }

        public Task<IUserMessage> SendFileAsync(string filePath, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, bool isSpoiler = false, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null)
        {
            return ((IMessageChannel)Channel).SendFileAsync(filePath, text, isTTS, embed, options, isSpoiler, allowedMentions, messageReference, components, stickers, embeds);
        }

        public Task<IUserMessage> SendFileAsync(Stream stream, string filename, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, bool isSpoiler = false, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null)
        {
            return ((IMessageChannel)Channel).SendFileAsync(stream, filename, text, isTTS, embed, options, isSpoiler, allowedMentions, messageReference, components, stickers, embeds);
        }

        public Task<IUserMessage> SendFileAsync(FileAttachment attachment, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null)
        {
            return ((IMessageChannel)Channel).SendFileAsync(attachment, text, isTTS, embed, options, allowedMentions, messageReference, components, stickers, embeds);
        }

        public Task<IUserMessage> SendFilesAsync(IEnumerable<FileAttachment> attachments, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null)
        {
            return ((IMessageChannel)Channel).SendFilesAsync(attachments, text, isTTS, embed, options, allowedMentions, messageReference, components, stickers, embeds);
        }

        public Task<IMessage> GetMessageAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IMessageChannel)Channel).GetMessageAsync(id, mode, options);
        }

        public IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetMessagesAsync(int limit = 100, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IMessageChannel)Channel).GetMessagesAsync(limit, mode, options);
        }

        public IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetMessagesAsync(ulong fromMessageId, Direction dir, int limit = 100, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IMessageChannel)Channel).GetMessagesAsync(fromMessageId, dir, limit, mode, options);
        }

        public IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetMessagesAsync(IMessage fromMessage, Direction dir, int limit = 100, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IMessageChannel)Channel).GetMessagesAsync(fromMessage, dir, limit, mode, options);
        }

        public Task<IReadOnlyCollection<IMessage>> GetPinnedMessagesAsync(RequestOptions options = null)
        {
            return ((IMessageChannel)Channel).GetPinnedMessagesAsync(options);
        }

        public Task DeleteMessageAsync(ulong messageId, RequestOptions options = null)
        {
            return ((IMessageChannel)Channel).DeleteMessageAsync(messageId, options);
        }

        public Task DeleteMessageAsync(IMessage message, RequestOptions options = null)
        {
            return ((IMessageChannel)Channel).DeleteMessageAsync(message, options);
        }

        public Task<IUserMessage> ModifyMessageAsync(ulong messageId, Action<MessageProperties> func, RequestOptions options = null)
        {
            return ((IMessageChannel)Channel).ModifyMessageAsync(messageId, func, options);
        }

        public Task TriggerTypingAsync(RequestOptions options = null)
        {
            return ((IMessageChannel)Channel).TriggerTypingAsync(options);
        }

        public IDisposable EnterTypingState(RequestOptions options = null)
        {
            return ((IMessageChannel)Channel).EnterTypingState(options);
        }

        public Task<ICategoryChannel> GetCategoryAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((INestedChannel)Channel).GetCategoryAsync(mode, options);
        }

        public Task SyncPermissionsAsync(RequestOptions options = null)
        {
            return ((INestedChannel)Channel).SyncPermissionsAsync(options);
        }

        public Task<IInviteMetadata> CreateInviteAsync(int? maxAge = 86400, int? maxUses = null, bool isTemporary = false, bool isUnique = false, RequestOptions options = null)
        {
            return ((INestedChannel)Channel).CreateInviteAsync(maxAge, maxUses, isTemporary, isUnique, options);
        }

        public Task<IInviteMetadata> CreateInviteToApplicationAsync(ulong applicationId, int? maxAge = 86400, int? maxUses = null, bool isTemporary = false, bool isUnique = false, RequestOptions options = null)
        {
            return ((INestedChannel)Channel).CreateInviteToApplicationAsync(applicationId, maxAge, maxUses, isTemporary, isUnique, options);
        }

        public Task<IInviteMetadata> CreateInviteToApplicationAsync(DefaultApplications application, int? maxAge = 86400, int? maxUses = null, bool isTemporary = false, bool isUnique = false, RequestOptions options = null)
        {
            return ((INestedChannel)Channel).CreateInviteToApplicationAsync(application, maxAge, maxUses, isTemporary, isUnique, options);
        }

        public Task<IInviteMetadata> CreateInviteToStreamAsync(IUser user, int? maxAge = 86400, int? maxUses = null, bool isTemporary = false, bool isUnique = false, RequestOptions options = null)
        {
            return ((INestedChannel)Channel).CreateInviteToStreamAsync(user, maxAge, maxUses, isTemporary, isUnique, options);
        }

        public Task<IReadOnlyCollection<IInviteMetadata>> GetInvitesAsync(RequestOptions options = null)
        {
            return ((INestedChannel)Channel).GetInvitesAsync(options);
        }

        public Task ModifyAsync(Action<GuildChannelProperties> func, RequestOptions options = null)
        {
            return ((IGuildChannel)Channel).ModifyAsync(func, options);
        }

        public OverwritePermissions? GetPermissionOverwrite(IRole role)
        {
            return ((IGuildChannel)Channel).GetPermissionOverwrite(role);
        }

        public OverwritePermissions? GetPermissionOverwrite(IUser user)
        {
            return ((IGuildChannel)Channel).GetPermissionOverwrite(user);
        }

        public Task RemovePermissionOverwriteAsync(IRole role, RequestOptions options = null)
        {
            return ((IGuildChannel)Channel).RemovePermissionOverwriteAsync(role, options);
        }

        public Task RemovePermissionOverwriteAsync(IUser user, RequestOptions options = null)
        {
            return ((IGuildChannel)Channel).RemovePermissionOverwriteAsync(user, options);
        }

        public Task AddPermissionOverwriteAsync(IRole role, OverwritePermissions permissions, RequestOptions options = null)
        {
            return ((IGuildChannel)Channel).AddPermissionOverwriteAsync(role, permissions, options);
        }

        public Task AddPermissionOverwriteAsync(IUser user, OverwritePermissions permissions, RequestOptions options = null)
        {
            return ((IGuildChannel)Channel).AddPermissionOverwriteAsync(user, permissions, options);
        }

        public IAsyncEnumerable<IReadOnlyCollection<IGuildUser>> GetUsersAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuildChannel)Channel).GetUsersAsync(mode, options);
        }

        public Task<IGuildUser> GetUserAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return ((IGuildChannel)Channel).GetUserAsync(id, mode, options);
        }

        IAsyncEnumerable<IReadOnlyCollection<IUser>> IChannel.GetUsersAsync(CacheMode mode, RequestOptions options)
        {
            return ((IChannel)Channel).GetUsersAsync(mode, options);
        }

        Task<IUser> IChannel.GetUserAsync(ulong id, CacheMode mode, RequestOptions options)
        {
            return ((IChannel)Channel).GetUserAsync(id, mode, options);
        }

        public Task DeleteAsync(RequestOptions options = null)
        {
            return ((IDeletable)Channel).DeleteAsync(options);
        }



        #endregion
    }

    internal class LazyIUserMessage : LazyObject, IUserMessage
    {
        public LazyIUserMessage(ulong _guildId, ulong _channelId, ulong _messageId) : base(_messageId)
        {
            guildId = _guildId;
            channelId = _channelId;
            Program.LogVerbose($"Lazily making message {ToString()}", "LazyGuildTChannel");
        }
        private ulong guildId;
        private ulong channelId;

        public override string ToString()
        {
            return $"{guildId}.{channelId}.{Id}";
        }

        private IUserMessage _message;

        public IUserMessage Message
        {
            get
            {
                if (_message == null)
                {
                Program.LogVerbose($"Lazily loading message {ToString()}", "LazyGuildTChannel");
                    var guild = Program.Client.GetGuild(guildId);
                    if (guild == null)
                    {
                        var chnl = Program.Client.GetUser(channelId).CreateDMChannelAsync().Result;
                        _message = chnl.GetMessageAsync(Id).Result as IUserMessage;
                    }
                    else
                    {
                        _message = guild.GetTextChannel(channelId)?.GetMessageAsync(Id)?.Result as IUserMessage;
                    }
                }
                return _message;
            }
        }
        #region IUserMessage items
        public IUserMessage ReferencedMessage => Message.ReferencedMessage;

        public MessageType Type => Message.Type;

        public MessageSource Source => Message.Source;

        public bool IsTTS => Message.IsTTS;

        public bool IsPinned => Message.IsPinned;

        public bool IsSuppressed => Message.IsSuppressed;

        public bool MentionedEveryone => Message.MentionedEveryone;

        public string Content => Message.Content;

        public string CleanContent => Message.CleanContent;

        public DateTimeOffset Timestamp => Message.Timestamp;

        public DateTimeOffset? EditedTimestamp => Message.EditedTimestamp;

        public IMessageChannel Channel => Message.Channel;

        public IUser Author => Message.Author;

        public IReadOnlyCollection<IAttachment> Attachments => Message.Attachments;

        public IReadOnlyCollection<IEmbed> Embeds => Message.Embeds;

        public IReadOnlyCollection<ITag> Tags => Message.Tags;

        public IReadOnlyCollection<ulong> MentionedChannelIds => Message.MentionedChannelIds;

        public IReadOnlyCollection<ulong> MentionedRoleIds => Message.MentionedRoleIds;

        public IReadOnlyCollection<ulong> MentionedUserIds => Message.MentionedUserIds;

        public MessageActivity Activity => Message.Activity;

        public MessageApplication Application => Message.Application;

        public MessageReference Reference => Message.Reference;

        public IReadOnlyDictionary<IEmote, ReactionMetadata> Reactions => Message.Reactions;

        public IReadOnlyCollection<IMessageComponent> Components => Message.Components;

        public IReadOnlyCollection<IStickerItem> Stickers => Message.Stickers;

        public MessageFlags? Flags => Message.Flags;

        public IMessageInteraction Interaction => Message.Interaction;

        public DateTimeOffset CreatedAt => Message.CreatedAt;

        public Task ModifyAsync(Action<MessageProperties> func, RequestOptions options = null)
        {
            return Message.ModifyAsync(func, options);
        }

        public Task PinAsync(RequestOptions options = null)
        {
            return Message.PinAsync(options);
        }

        public Task UnpinAsync(RequestOptions options = null)
        {
            return Message.UnpinAsync(options);
        }

        public Task CrosspostAsync(RequestOptions options = null)
        {
            return Message.CrosspostAsync(options);
        }

        public string Resolve(TagHandling userHandling = TagHandling.Name, TagHandling channelHandling = TagHandling.Name, TagHandling roleHandling = TagHandling.Name, TagHandling everyoneHandling = TagHandling.Ignore, TagHandling emojiHandling = TagHandling.Name)
        {
            return Message.Resolve(userHandling, channelHandling, roleHandling, everyoneHandling, emojiHandling);
        }

        public Task AddReactionAsync(IEmote emote, RequestOptions options = null)
        {
            return Message.AddReactionAsync(emote, options);
        }

        public Task RemoveReactionAsync(IEmote emote, IUser user, RequestOptions options = null)
        {
            return Message.RemoveReactionAsync(emote, user, options);
        }

        public Task RemoveReactionAsync(IEmote emote, ulong userId, RequestOptions options = null)
        {
            return Message.RemoveReactionAsync(emote, userId, options);
        }

        public Task RemoveAllReactionsAsync(RequestOptions options = null)
        {
            return Message.RemoveAllReactionsAsync(options);
        }

        public Task RemoveAllReactionsForEmoteAsync(IEmote emote, RequestOptions options = null)
        {
            return Message.RemoveAllReactionsForEmoteAsync(emote, options);
        }

        public IAsyncEnumerable<IReadOnlyCollection<IUser>> GetReactionUsersAsync(IEmote emoji, int limit, RequestOptions options = null)
        {
            return Message.GetReactionUsersAsync(emoji, limit, options);
        }

        public Task DeleteAsync(RequestOptions options = null)
        {
            return Message.DeleteAsync(options);
        }


#endregion
    }

}
