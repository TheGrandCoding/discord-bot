using Discord;
using Discord.WebSocket;
using DiscordBot.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Classes
{
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore, MemberSerialization = MemberSerialization.OptIn)]
    public class BotUser //: IUser
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
            GeneratedUser = true;
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

        [JsonIgnore]
        public string RedirectUrl { get; set; }

        [JsonProperty("options")]
        public BotUserOptions Options { get; set; } = BotUserOptions.Default;

        [JsonProperty("vpnlast")]
        public Dictionary<ulong, DateTime> LastVisitVPN { get; set; } = new Dictionary<ulong, DateTime>();

        [JsonIgnore]
        public string MLAPIPassword {  get
            {
                return Tokens.FirstOrDefault(x => x.Name == AuthToken.LoginPassword)?.Value;
            } set
            {
                if(value == null)
                {
                    Tokens.RemoveAll(x => x.Name == AuthToken.LoginPassword);
                } else
                {
                    var tkn = Tokens.FirstOrDefault(x => x.Name == AuthToken.LoginPassword);
                    if(tkn == null)
                    {
                        tkn = new AuthToken(AuthToken.LoginPassword);
                        Tokens.Add(tkn);
                    }
                    tkn.SetHashValue(value);
                }
                Tokens.FirstOrDefault(x => x.Name == AuthToken.HttpFullAccess)?.Regenerate();
            }
        }

        /// <summary>
        /// Code: Name of subject
        /// </summary>
        [JsonProperty("subjs")]
        public Dictionary<string, string> Classes { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Indicates this user is specifically for an internal usage
        /// </summary>
        [JsonProperty("builtin")]
        public bool ServiceUser { get; set; } = false;
        /// <summary>
        /// Indicates this user has been automatically created and is not tied to a Discord account
        /// </summary>
        [JsonProperty("generated")]
        public bool GeneratedUser { get; set; } = false;

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
            : (((IUser)FirstValidUser)?.DiscriminatorValue ?? 0);
        public bool IsBot => ((IUser)FirstValidUser).IsBot;
        public bool IsWebhook => ((IUser)FirstValidUser).IsWebhook;
        public string Username => ((IUser)FirstValidUser)?.Username ?? null;
        public DateTimeOffset CreatedAt => ((IUser)FirstValidUser).CreatedAt;
        public string Mention => MentionUtils.MentionUser(Id);
        public IActivity Activity => ((IUser)FirstValidUser).Activity;
        public UserStatus Status => ((IUser)FirstValidUser).Status;
        public string GetAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
        {
            return ((IUser)FirstValidUser)?.GetAvatarUrl(format, size) ?? GetDefaultAvatarUrl();
        }
        public string GetDefaultAvatarUrl()
        {
            return CDN.GetDefaultUserAvatarUrl(DiscriminatorValue);
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

        public string Name => OverrideName ?? FirstValidUser?.Nickname ?? FirstValidUser?.Username ?? Id.ToString();

        [JsonProperty("v")]
        public bool IsVerified { get; set; }

        [JsonProperty("isa", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsApproved { get; set; }

        [JsonIgnore]
        public string VerifiedEmail { get; set; }

        [JsonProperty("edu", NullValueHandling = NullValueHandling.Ignore)]
        public int? EdulinkId { get; set; }

        SavedReason _reason = new SavedReason(null);

        [JsonIgnore]
        public string Reason
        {
            get
            {
                if (_reason.Reason == null || _reason.Expired)
                    return null;
                return _reason.Reason;
            }
            set
            {
                _reason = new SavedReason(value);
            }
        }
    }

    public struct SavedReason
    {
        public SavedReason(string reason)
        {
            Reason = reason;
            Set = DateTime.Now.AddMinutes(-1);
        }
        public string Reason { get; }
        public DateTime Set { get; }
        public bool Expired => Set.AddMinutes(15) < DateTime.Now;
    }
}
