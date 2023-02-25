using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

namespace DiscordBot.Classes
{
    public class BotDbContext : DbContext
    {
        public static BotDbContext Get()
        {
            return Program.Services.GetRequiredService<BotDbContext>();
        }
        public DbSet<BotDbUser> Users { get; set; } 
        public DbSet<BotDbAuthToken> AuthTokens { get; set; }
        public DbSet<BotDbAuthSession> AuthSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<BotDbPermission>()
                .Property(x => x.PermNode)
                .HasConversion(
                    toProvider => toProvider.RawNode,
                    fromProvider => Perm.Parse(fromProvider)
                );
        }


        public async Task<Result<BotDbUser>> AttemptLoginAsync(string username, string password)
        {
            var user = await Users.FirstOrDefaultAsync(x => x.Name == username);
            if (user == null) 
                return new("No user exists by that username");

            if (string.IsNullOrWhiteSpace(user.Connections.PasswordHash))
                return new("Incorrect password");

            var valid = PasswordHash.ValidatePassword(password, user.Connections.PasswordHash);
            if (valid) return new Result<BotDbUser>(user);
            return new("Incorrect password");
        }

        public async Task<BotDbUser> GetUserAsync(uint id)
        {
            return await Users.FindAsync(id);
        }
        public async Task<Result<BotDbUser>> GetUserFromDiscord(Discord.IUser discordUser, bool createIfNotExist)
        {
            var idstr = discordUser.Id.ToString();
            var user = await Users.FirstOrDefaultAsync(x => x.Connections.DiscordId == idstr);
            if(user == null) return new(user);
            if (!createIfNotExist) return new("No user has linked that account");

            user = new BotDbUser();
            user.Name = discordUser.Username;
            user.Connections = new BotDbConnections()
            {
                DiscordId = idstr
            };
            await Users.AddAsync(user);
            return new(user);
        }
        public async Task<Result<BotDbUser>> GetUserFromDiscord(ulong dsId, bool createIfNotExist)
        {
            var user = await Program.Client.GetUserAsync(dsId);
            if (user != null)
                return await GetUserFromDiscord(user, createIfNotExist);
            var str = dsId.ToString();
            var fetch = await Users.FirstOrDefaultAsync(x => x.Connections.DiscordId == str);
            if (fetch != null)
                return new(fetch);
            return new("Could not find user in DB");
        }
        public async Task<BotDbAuthSession> GetSessionAsync(string token)
        {
            return await AuthSessions.FindAsync(token);
        }
        public async Task<BotDbAuthToken> GetTokenAsync(string token)
        {
            var BotDbAuthToken = await AuthTokens.FindAsync(token);
            return BotDbAuthToken;
        }
        public async Task<BotDbAuthSession> GenerateNewSession(BotDbUser user, string ip, string ua, bool? forceApproved = null)
        {
            var auth = new BotDbAuthSession(ip, ua, forceApproved ?? false);
            auth.User = user;
            await AuthSessions.AddAsync(auth);
            await SaveChangesAsync();
            return auth;
        }
    }

    public class BotDbUser
    {
        public uint Id { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Whether MLAPI's owner has authorised this user to access the website
        /// </summary>
        public bool? Approved { get; set; }

        /// <summary>
        /// Whether the user has made a successful visit to :url:/verify
        /// </summary>
        public bool Verified { get; set; }

        public string RedirectUrl { get; set; }

        /// <summary>
        /// Default penalty reason
        /// </summary>
        public string Reason { get; set; }

        public BotDbConnections Connections { get; set; }
        public BotDbUserOptions Options { get; set; }

        public List<BotDbAuthSession> AuthSessions { get; set; }
        public List<BotDbAuthToken> AuthTokens { get; set; }
        public List<BotDbApprovedIP> ApprovedIPs { get; set; }
        public List<BotDbPermission> Permissions { get; set; }

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
        public string PasswordHash { get; set; }

        public string DiscordId { get; set; }

        private Discord.WebSocket.SocketUser _discord;
        [NotMapped]
        public Discord.WebSocket.SocketUser Discord { get
            {
                if (DiscordId == null) return null;
                return _discord ??= Program.Client.GetUser(ulong.Parse(DiscordId));
            } }
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
        public string Token { get; set; }

        public DateTime StartedAt { get; set; }
        public string IP { get; set; }
        public string UserAgent { get; set; }
        public bool Approved { get; set; }


        public uint UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public BotDbUser User { get; set; }
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
        public string Token { get; set; }

        public string Name { get; set; }

        [Column("Scopes")]
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
        public BotDbUser User { get; set; }
    }

    public class BotDbApprovedIP
    {
        public uint UserId { get; set; }
        public BotDbUser User { get; set; }

        public string IP { get; set; }
    }
    public class BotDbPermission
    {
        public BotDbPermission() { }
        public BotDbPermission(BotDbUser user, Perm node)
        {
            UserId = user.Id;
            User = user;
            PermNode = node;
        }
        public uint UserId { get; set; }
        public BotDbUser User { get; set; }

        public Perm PermNode { get; set; }
    }

}
