using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Classes
{
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore, MemberSerialization = MemberSerialization.OptIn)]
    public class BotUser : IUser
    {
        [JsonConstructor]
        private BotUser()
        {
        }
        public BotUser(IUser user)
        {
            Id = user.Id;
        }
        public BotUser(WebUser webuser)
        {
            Id = webuser.Id;
            // Since we can't gurantee webusers are in any guild with bot, we must 
            // set overrides
            OverrideName = webuser.Username;
            OverrideDiscriminator = webuser.Discriminator;
        }
        public BotUser(ulong id)
        {
            Id = id;
        }

        [JsonProperty("id")]
        public ulong Id { get; set; }
        [JsonProperty("tokens")]
        public List<AuthToken> Tokens { get; set; } = new List<AuthToken>();
        [JsonProperty("perms")]
        public List<Perm> Permissions { get; set; } = new List<Perm>();
        [JsonProperty("builtin")]
        public bool BuiltIn { get; set; } = false;

        public SocketGuildUser FirstValidUser 
        { 
            get
            {
                if (Program.Client == null)
                {
                    Program.LogMsg($"Attempted to access FirstValidUser before Client is set", source:"BotUser", sev:LogSeverity.Warning);
                    return null;
                }
                foreach (var g in Program.Client.Guilds)
                {
                    var u = g.GetUser(Id);
                    if (u != null)
                        return u;
                }
                return null;
            } 
        }

        #region IUser Implementations
        public string AvatarId => ((IUser)FirstValidUser).AvatarId;
        public string Discriminator => DiscriminatorValue.ToString();
        public ushort DiscriminatorValue => OverrideDiscriminator.HasValue 
            ? OverrideDiscriminator.Value
            : ((IUser)FirstValidUser).DiscriminatorValue;
        public bool IsBot => ((IUser)FirstValidUser).IsBot;
        public bool IsWebhook => ((IUser)FirstValidUser).IsWebhook;
        public string Username => ((IUser)FirstValidUser).Username;
        public DateTimeOffset CreatedAt => ((IUser)FirstValidUser).CreatedAt;
        public string Mention => ((IUser)FirstValidUser).Mention;
        public IActivity Activity => ((IUser)FirstValidUser).Activity;
        public UserStatus Status => ((IUser)FirstValidUser).Status;
        public string GetAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
        {
            return ((IUser)FirstValidUser).GetAvatarUrl(format, size);
        }
        public string GetDefaultAvatarUrl()
        {
            return ((IUser)FirstValidUser).GetDefaultAvatarUrl();
        }
        public Task<IDMChannel> GetOrCreateDMChannelAsync(RequestOptions options = null)
        {
            return ((IUser)FirstValidUser).GetOrCreateDMChannelAsync(options);
        }
        public IImmutableSet<ClientType> ActiveClients => ((IUser)FirstValidUser).ActiveClients;
        #endregion

        [JsonProperty("oname", NullValueHandling = NullValueHandling.Ignore)]
        public string OverrideName { get; set; } = null;

        [JsonProperty("oshort", NullValueHandling = NullValueHandling.Ignore)]
        public ushort? OverrideDiscriminator { get; set; } = null;

        public string Name => OverrideName ?? FirstValidUser?.Nickname ?? Username ?? Id.ToString();

        [JsonProperty("mail", NullValueHandling = NullValueHandling.Ignore)]
        public string VerifiedEmail { get; set; }
    }
}
