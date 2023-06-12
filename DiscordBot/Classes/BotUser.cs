using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordBot.Classes
{

    public class BotDbUser
    {
        public uint Id { get; set; }
        [MaxLength(32)]
        public string Name { get; set; }

        private string _displayName;

        [BackingField(nameof(_displayName))]
        public string DisplayName { get
            {
                return _displayName ?? Name ?? $"({Id})";
            } set
            {
                _displayName = value;
            }
        }
        [NotMapped]
        public bool HasDisplayName => _displayName != null;

        /// <summary>
        /// Whether MLAPI's owner has authorised this user to access the website
        /// </summary>
        public bool? Approved { get; set; }

        /// <summary>
        /// Whether the user has made a successful visit to :url:/verify
        /// </summary>
        public bool Verified { get; set; }

        [MaxLength(1024)]
        public string RedirectUrl { get; set; }

        /// <summary>
        /// Default penalty reason
        /// </summary>
        [MaxLength(128)]
        public string Reason { get; set; }

        [Required]
        public BotDbConnections Connections { get; set; } = new();
        [Required]
        public BotDbUserOptions Options { get; set; } = new();
        public BotDbInstagram Instagram { get; set; }
        public BotDbFacebook Facebook { get; set; }
        public BotDbTikTok TikTok { get; set; }

        public BotRepublishRoles RepublishRole { get; set; } = BotRepublishRoles.None;

        public virtual List<BotDbAuthSession> AuthSessions { get; set; } = new();
        public virtual List<BotDbAuthToken> AuthTokens { get; set; } = new();
        public virtual List<BotDbApprovedIP> ApprovedIPs { get; set; } = new();
        public virtual List<BotDbPermission> Permissions { get; set; } = new();

        public void WithPerm(Perm perm)
        {
            var bot = new BotDbPermission()
            {
                User = this,
                UserId = Id,
                PermNode = perm
            };
            Permissions.Add(bot);
        }
        public int RemovePerm(Perm perm)
        {
            return Permissions.RemoveAll(x => x.PermNode.RawNode == perm.RawNode);
        }

        public void WithApprovedIP(string ip)
        {
            if (!System.Net.IPAddress.TryParse(ip, out _))
                throw new ArgumentException("Not a valid IP");
            var dbi = new BotDbApprovedIP()
            {
                IP = ip,
                User = this,
                UserId = Id
            };
            ApprovedIPs.Add(dbi);
        }

    }

    [Owned]
    public class BotDbConnections
    {
        [MaxLength(128)]
        public string PasswordHash { get; set; }

        [MaxLength(32)]
        public string DiscordId { get; set; }

        private Discord.WebSocket.SocketUser _discord;
        [NotMapped]
        public Discord.WebSocket.SocketUser Discord { get
            {
                if (DiscordId == null) return null;
                return _discord ??= Program.Client.GetUser(ulong.Parse(DiscordId));
            } }
    }

    [Owned]
    public class BotDbOAuthAccount
    {
        public string AccountId { get; set; }
        public string AccessToken { get; set; }
        public DateTime ExpiresAt { get; set; }

        public bool IsValid(out bool expired)
        {
            expired = false;
            if (string.IsNullOrWhiteSpace(AccountId) || string.IsNullOrWhiteSpace(AccessToken))
                return false;
            expired = ExpiresAt < DateTime.UtcNow;
            return !expired;
        }
    }

    [Owned]
    public class BotDbInstagram : BotDbOAuthAccount
    {
        public ExternalAPIs.InstagramClient CreateClient(System.Net.Http.HttpClient http)
        {
            return ExternalAPIs.InstagramClient.Create(AccessToken, AccountId, ExpiresAt, http);
        }
    }

    [Owned]
    public class BotDbTikTok : BotDbOAuthAccount
    {
        public string RefreshToken { get; set; }
        public DateTime RefreshExpiresAt { get; set; }
        public ExternalAPIs.TikTokClient CreateClient(System.Net.Http.HttpClient http)
        {
            return ExternalAPIs.TikTokClient.Create(AccessToken, ExpiresAt, RefreshToken, RefreshExpiresAt, http);
        }
    }
    [Owned]
    public class BotDbFacebook : BotDbOAuthAccount
    {
    }


    public class BotDbAuthSession
    {
        public const string CookieName = "session";
        public BotDbAuthSession(string ip, string ua, bool v)
        {
            IP = ip;
            UserAgent= ua;
            Approved = v;
            Token = "s_" + PasswordHash.RandomToken(32);
            StartedAt = DateTime.Now;
        }
        public BotDbAuthSession() { }

        [Key]
        [MaxLength(64)]
        public string Token { get; set; }

        public DateTime StartedAt { get; set; }
        [MaxLength(16)]
        public string IP { get; set; }
        [MaxLength(512)]
        public string UserAgent { get; set; }
        public bool Approved { get; set; }


        public uint UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public virtual BotDbUser User { get; set; }
    }
    public class BotDbAuthToken
    {
        public const string TimeToken = "timetracker";

        public BotDbAuthToken() { }
        public static BotDbAuthToken Create(BotDbUser user, string name, int length = 32, params string[] scopes)
        {
            return new BotDbAuthToken()
            {
                User = user,
                UserId = user.Id,
                Name = name,
                Scopes = scopes,
                Token = PasswordHash.RandomToken(length)
            };
        }

        [Key]
        [MaxLength(64)]
        public string Token { get; set; }

        [MaxLength(32)]
        public string Name { get; set; }

        [Column("Scopes")]
        [MaxLength(128)]
        string _scopes;

        [NotMapped]
        public string[] Scopes 
        { 
            get
            {
                return (_scopes ?? "").Split(';');
            } 
            set
            {
                _scopes = string.Join(';', value);
            }
        }
        public uint UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public virtual BotDbUser User { get; set; }
    }

    [PrimaryKey(nameof(UserId), nameof(IP))]
    public class BotDbApprovedIP
    {
        public uint UserId { get; set; }
        public virtual BotDbUser User { get; set; }

        public string IP { get; set; }
    }
    [PrimaryKey(nameof(UserId), nameof(PermNode))]
    public class BotDbPermission
    {
        public BotDbPermission() { }
        public static BotDbPermission Create(BotDbUser user, Perm node)
        {
            return new BotDbPermission()
            {
                UserId = user.Id,
                User = user,
                PermNode = node
            };
        }
        public uint UserId { get; set; }
        public virtual BotDbUser User { get; set; }

        [MaxLength(64)]
        public Perm PermNode { get; set; }
    }
    public enum BotRepublishRoles
    {
        None        = 0b000,
        Provider    = 0b001, // 1
        Approver    = 0b010, // 2
        Admin       = 0b111  // 7
    }
}
