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
        static int _disposed = 0;
        int Id = 0;
        public static BotDbContext Get()
        {
            var d = Program.Services.GetRequiredService<BotDbContext>();
            d.Id = System.Threading.Interlocked.Increment(ref _id);
            Program.LogDebug($"Created DB {d.Id}", "BotDbCtx");
            return d;
        }
        public override void Dispose()
        {
            int count = System.Threading.Interlocked.Increment(ref _disposed);
            Program.LogWarning($"Disposing DB {Id}; count: {count}", "BotDbCtx");
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
        public async Task<Result<BotDbUser>> AttemptRegisterAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)
                || password.Length < 8)
                return new("Username or password invalid.");
            if (await Program.IsPasswordLeaked(password))
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
