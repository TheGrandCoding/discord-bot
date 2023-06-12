using DiscordBot.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Classes.DbContexts
{
    public class BotDbContext : AbstractDbBase
    {
        private static int _count = 0;
        private static SemaphoreSlim _semaphore = new(1, 1);
        protected override int _lockCount { get => _count; set => _count = value; }
        protected override SemaphoreSlim _lock => _semaphore;

        public DbSet<BotDbUser> Users { get; set; }
        public DbSet<BotDbAuthToken> AuthTokens { get; set; }
        public DbSet<BotDbAuthSession> AuthSessions { get; set; }

        public DbSet<PublishPost> Posts { get; set; }
        public DbSet<PublishBase> PostPlatforms { get; set; }

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
            mb.Entity<PublishBase>()
                .HasDiscriminator(x => x.Platform)
                .HasValue<PublishDefault>(PublishPlatform.Default)
                .HasValue<PublishInstagram>(PublishPlatform.Instagram)
                .HasValue<PublishDiscord>(PublishPlatform.Discord)
                .HasValue<PublishTikTok>(PublishPlatform.TikTok);
            mb.Entity<PublishPost>(post =>
            {
                post.HasMany(p => p.Platforms)
                    .WithOne()
                    .HasForeignKey(p => new { p.PostId })
                    .HasPrincipalKey(p => new { p.Id });
                post.Navigation(p => p.Platforms)
                    .AutoInclude();
            });
            mb.Entity<PublishBase>(pb =>
            {
                pb.HasKey(p => new { p.PostId, p.Platform });
                pb.HasMany(p => p.Media)
                    .WithOne()
                    .HasForeignKey(p => new { p.PostId, p.Platform })
                    .HasPrincipalKey(p => new { p.PostId, p.Platform });
                pb.Navigation(p => p.Media)
                    .AutoInclude();
            });
            mb.Entity<PublishMedia>(pm =>
            {
                pm.HasKey(p => p.Id);
                pm.HasIndex(p => new { p.PostId, p.Platform });
            });

        }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
#if DEBUG
            options.EnableSensitiveDataLogging();
#endif
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
            return await WithLock<Task<Result<BotDbUser>>>(async () =>
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

        public Task<BotDbUser[]> GetUsersWithExternal()
        {
            return WithLock(async () =>
            {
                return await Users.Where(x => x.Facebook.AccountId != null || x.Instagram.AccountId != null).ToArrayAsync();
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
            WithLock(() =>
            {
                Users.Remove(user);
            });
            return Task.CompletedTask;
        }
        public Task<Result<BotDbUser>> GetUserFromDiscord(Discord.IUser discordUser, bool createIfNotExist)
        {
            return WithLock<Task<Result<BotDbUser>>>(async () =>
            {
                var idstr = discordUser.Id.ToString();
                var user = await Users.FirstOrDefaultAsync(x => x.Connections.DiscordId == idstr);
                if (user != null) return new(user);
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
        public Task<BotDbUser> GetUserByTikTok(string tiktokId, bool createIfNotExists)
        {
            return WithLock(async () =>
            {
                var existing = await Users.FirstOrDefaultAsync(x => x.Facebook.AccountId == tiktokId);
                if (existing != null) return existing;

                existing = new BotDbUser();
                existing.Name = "tk_" + tiktokId;
                existing.Connections = new();
                existing.Facebook = new BotDbFacebook()
                {
                    AccountId = tiktokId
                };
                await Users.AddAsync(existing);
                return existing;
            });
        }

        public Task<BotDbAuthSession> GetSessionAsync(string token)
        {
            return WithLock(async () =>
            {
                return await AuthSessions
                    .Include(x => x.User)
                    .FirstOrDefaultAsync(t => t.Token == token);
            });
        }
        public void RemoveSessionAsync(BotDbAuthSession session)
        {
            WithLock(() =>
            {
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
            return WithLock(async () =>
            {
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

        public Task CreateNewPost(PublishPost post)
        {
            return WithLock(async () =>
            {
                await Posts.AddAsync(post);
            });
        }
        public Task<PublishPost> GetPost(uint id)
        {
            return WithLock(async () =>
            {
                return await Posts.FindAsync(id);
            });
        }
        public IEnumerable<PublishPost> GetAllPosts(bool unapprovedOnly = true)
        {
            var ls = new List<PublishPost>();
            WithLock(() =>
            {
                if (unapprovedOnly)
                    ls = Posts.Where(x => x.ApprovedById == null).ToList();
                else
                    ls = Posts.ToList();
            });
            return ls;
        }
    }
}
