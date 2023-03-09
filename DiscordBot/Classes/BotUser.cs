using DiscordBot.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Classes
{
    public class BotDbContext : DbContext
    {
        static int _id = 0;
        int Id = 0;
        string _reason;
        public void SetReason(string reason)
        {
            if(Id == 0)
            {
                Id = System.Threading.Interlocked.Increment(ref _id);
                Program.LogDebug($"Created DB {Id}/{reason}", "BotDbCtx");
                _reason = reason;
            } else
            {
                _reason = (_reason ?? "") + "+" + reason;
                Program.LogDebug($"Re-used DB {Id}/{_reason}", "BotDbCtx");
            }
        }
        public override void Dispose()
        {
            Program.LogWarning($"Disposing DB {Id}/{_reason}", "BotDbCtx");
            base.Dispose();
        }
        public static SemaphoreSlim _lock = new(1);
        public DbSet<BotDbUser> Users { get; set; } 
        public DbSet<BotDbAuthToken> AuthTokens { get; set; }
        public DbSet<BotDbAuthSession> AuthSessions { get; set; }

        public async Task<T> WithLock<T>(Func<Task<T>> action)
        {
            try
            {
                await _lock.WaitAsync();
                var task = action();
                return await task;
            } finally
            {
                _lock.Release();
            }
        }
        public Task WithLock(Action action)
        {
            try
            {
                _lock.Wait();
                action();
                return Task.CompletedTask;
            }
            finally
            {
                _lock.Release();
            }
        }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<BotDbPermission>()
                .Property(x => x.PermNode)
                .HasConversion(
                    toProvider => toProvider.RawNode,
                    fromProvider => Perm.Parse(fromProvider)
                );
            mb.Entity<BotDbUser>(b =>
            {
                b.Navigation(p => p.AuthSessions).AutoInclude();
                b.Navigation(p => p.AuthTokens).AutoInclude();
                b.Navigation(p => p.ApprovedIPs).AutoInclude();
                b.Navigation(p => p.Permissions).AutoInclude();

                b.HasIndex(p => p.Name).IsUnique();

                b.OwnsOne(p => p.Facebook).HasIndex(i => i.AccountId).IsUnique();
                b.OwnsOne(p => p.Instagram).HasIndex(i => i.AccountId).IsUnique();
            });
        }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.WithSQLConnection("botdb", true);
        }

        public Task<Result<BotDbUser>> AttemptLoginAsync(string username, string password)
        {
            return WithLock(async () =>
            {
                var user = await Users.FirstOrDefaultAsync(x => x.Name == username);
                if (user == null)
                    return new("No user exists by that username");

                if (string.IsNullOrWhiteSpace(user.Connections.PasswordHash))
                    return new("Incorrect password");

                var valid = PasswordHash.ValidatePassword(password, user.Connections.PasswordHash);
                if (valid) return new Result<BotDbUser>(user);
                return new("Incorrect password");
            });
        }
        public async Task<Result<BotDbUser>> AttemptRegisterAsync(string username, string password, IServiceProvider services)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)
                || password.Length < 8)
                return new("Username or password invalid.");
            if (await Program.IsPasswordLeaked(services.GetRequiredService<BotHttpClient>(), password))
                return new("Password is known to be compromised.");
            return await WithLock<Result<BotDbUser>>(async () =>
            {
                var existing = await Users.FirstOrDefaultAsync(x => x.Name == username);
                if (existing != null)
                    return new("A user already exists by this username");
                var buser = new BotDbUser()
                {
                    Name = username,
                    Connections = new BotDbConnections()
                    {
                        PasswordHash = PasswordHash.HashPassword(password)
                    }
                };
                await Users.AddAsync(buser);
                return new(buser);
            });
        }


        public Task<BotDbUser> GetUserAsync(uint id)
        {
            return WithLock(async () => await Users.FindAsync(id));
        }
        public Task DeleteUserAsync(uint id)
        {
            return WithLock(async () =>
            {
                var u = await Users.FindAsync(id);
                Users.Remove(u);
                return Task.CompletedTask;
            });
        }
        public Task DeleteUserAsync(BotDbUser user)
        {
            WithLock(() => {
                Users.Remove(user);
            });
            return Task.CompletedTask;
        }
        public Task<Result<BotDbUser>> GetUserFromDiscord(Discord.IUser discordUser, bool createIfNotExist)
        {
            return WithLock<Result<BotDbUser>>(async () =>
            {
                var idstr = discordUser.Id.ToString();
                var user = await Users.FirstOrDefaultAsync(x => x.Connections.DiscordId == idstr);
                if(user != null) return new(user);
                if (!createIfNotExist) return new("No user has linked that account");

                user = new BotDbUser();
                user.Name = discordUser.Username;
                var conns = new BotDbConnections()
                {
                    DiscordId = idstr
                };
                user.Connections = conns;
                await Users.AddAsync(user);
                return new(user);
            });
        }
        public Task<Result<BotDbUser>> GetUserFromDiscord(ulong dsId, bool createIfNotExist)
        {
            return WithLock(async () =>
            {
                var user = await Program.Client.GetUserAsync(dsId);
                if (user != null)
                    return await GetUserFromDiscord(user, createIfNotExist);
                var str = dsId.ToString();
                var fetch = await Users.FirstOrDefaultAsync(x => x.Connections.DiscordId == str);
                if (fetch != null)
                    return new(fetch);
                return new("Could not find user in DB");
            });
        }
        
        public Task<BotDbUser> GetUserByInstagram(string instagramId, bool createIfNotExists)
        {
            return WithLock(async () =>
            {
                var existing = await Users.FirstOrDefaultAsync(x => x.Instagram.AccountId == instagramId);
                if (existing != null) return existing;

                existing = new BotDbUser();
                existing.Name = "ig_" + instagramId;
                existing.Connections = new();
                existing.Instagram = new BotDbInstagram()
                {
                    AccountId = instagramId
                };
                await Users.AddAsync(existing);
                return existing;
            });
        }
        public Task<BotDbUser> GetUserByFacebook(string facebookId, bool createIfNotExists)
        {
            return WithLock(async () =>
            {
                var existing = await Users.FirstOrDefaultAsync(x => x.Facebook.AccountId == facebookId);
                if (existing != null) return existing;

                existing = new BotDbUser();
                existing.Name = "fb_" + facebookId;
                existing.Connections = new();
                existing.Facebook = new BotDbFacebook()
                {
                    AccountId = facebookId
                };
                await Users.AddAsync(existing);
                return existing;
            });
        }

        public Task<BotDbAuthSession> GetSessionAsync(string token)
        {
            return WithLock(async () => {
                return await AuthSessions
                    .Include(x => x.User)
                    .FirstOrDefaultAsync(t => t.Token == token);
            });
        }
        public Task RemoveSessionAsync(BotDbAuthSession session)
        {
            return WithLock(() => {
                AuthSessions.Remove(session);
            });
        }
        public Task RemoveSessionAsync(string token)
        {
            return WithLock(async () =>
            {
                var s = await AuthSessions.FindAsync(token);
                if (s != null)
                    AuthSessions.Remove(s);
            });
        }
        public Task<BotDbAuthToken> GetTokenAsync(string token)
        {
            return WithLock(async () => {
                var BotDbAuthToken = await AuthTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(t => t.Token == token);
                return BotDbAuthToken;
            });
        }
        public Task<BotDbAuthSession> GenerateNewSession(BotDbUser user, string ip, string ua, bool logoutOthers, bool? forceApproved = null)
        {
            return WithLock(async () =>
            {
                if (logoutOthers)
                {
                    await AuthSessions.Where(x => x.UserId == user.Id)
                        .ExecuteDeleteAsync();
                }
                var auth = new BotDbAuthSession(ip, ua, forceApproved ?? false);
                auth.User = user;
                await AuthSessions.AddAsync(auth);
                await SaveChangesAsync();
                return auth;
            });
        }
    }

    public class BotDbUser
    {
        public uint Id { get; set; }
        [MaxLength(32)]
        public string Name { get; set; }

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
    public class BotDbInstagram
    {
        public string AccountId { get; set; }
        public string AccessToken { get; set; }
        public DateTime ExpiresAt { get; set; }

        public bool IsInvalid()
        {
            return string.IsNullOrWhiteSpace(AccountId) || string.IsNullOrWhiteSpace(AccessToken) || ExpiresAt < DateTime.UtcNow;
        }
    }

    [Owned]
    public class BotDbFacebook
    {
        public string AccountId { get; set; }
        public string AccessToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsInvalid()
        {
            return string.IsNullOrWhiteSpace(AccountId) || string.IsNullOrWhiteSpace(AccessToken) || ExpiresAt < DateTime.UtcNow;
        }

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

}
