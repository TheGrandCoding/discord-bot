using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;

namespace DiscordBot.Services
{
    public class FoodService : Service
    {
        FoodDbContext DB()
        {
            return Program.Services.GetRequiredService<FoodDbContext>();
        }

        public Product GetProduct(string id)
        {
            using var db = DB();
            return db.GetProduct(id);
        }
        public Product AddProduct(string id, string name, string url)
        {
            using var db = DB();
            return db.AddProduct(id, name, url);
        }
        public InventoryItem AddInventoryItem(string productId, string inventryId, DateTime expires)
        {
            using var db = DB();
            return db.AddInventoryItem(productId, inventryId, expires);
        }
        public List<InventoryItem> GetInventory(string id)
        {
            using var db = DB();
            return db.GetInventory(id);
        }
    }

    public class FoodDbContext : DbContext
    {
        public FoodDbContext() { }
        public FoodDbContext(DbContextOptions<FoodDbContext> opt) : base(opt)
        {
        }
        public DbSet<Product> Products { get; set; }
        public DbSet<InventoryItem> Inventory { get; set; }
        public DbSet<HistoricItem> PreviousInventory { get; set; }

        public Product GetProduct(string id)
        {
            return Products.Find(id);
        }
        public Product AddProduct(string id, string name, string url)
        {
            var prod = new Product() { 
                Id = id,
                Name = name,
                Url = url
            };
            var x = Products.Add(prod);
            SaveChanges();
            return x.Entity;
        }

        public InventoryItem AddInventoryItem(string productId, string inventoryId, DateTime expires)
        {
            var inv = new InventoryItem()
            {
                ProductId = productId,
                InventoryId = inventoryId,
                AddedAt = DateTime.UtcNow,
                ExpiresAt = expires
            };
            var x = Inventory.Add(inv);
            SaveChanges();
            return x.Entity;
        }
        public List<InventoryItem> GetInventory(string id)
        {
            return Inventory
                .AsQueryable()
                .Where(x => x.InventoryId == id)
                .ToList();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
#if WINDOWS
            options.UseSqlServer(Program.getDbString("food"));
#else
                options.UseMySql(Program.getDbString("food"),
                    new MariaDbServerVersion(new Version(10, 3, 25)), mysqlOptions =>
                    {
                        mysqlOptions.CharSet(CharSet.Utf8Mb4);
                    });
#endif
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>()
                .HasKey(x => x.Id);
            modelBuilder.Entity<Product>()
                .HasMany(x => x.InventoryItems)
                .WithOne(x => x.Product)
                .HasForeignKey(x => x.ProductId);
            modelBuilder.Entity<Product>()
                .Navigation(x => x.InventoryItems)
                .AutoInclude();

            modelBuilder.Entity<InventoryItem>()
                .HasKey(x => x.Id);
            modelBuilder.Entity<InventoryItem>()
                .Property(x => x.Id)
                .UseIdentityColumn();
            modelBuilder.Entity<InventoryItem>()
                .Navigation(x => x.Product)
                .AutoInclude();

            modelBuilder.Entity<HistoricItem>()
                .HasKey(x => x.Id);
            modelBuilder.Entity<HistoricItem>()
                .Property(x => x.Id)
                .UseIdentityColumn();
        }
    }

    public class Product
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }

        public List<InventoryItem> InventoryItems { get; set; }

        public JObject ToJson()
        {
            var j = new JObject();
            j["id"] = Id;
            j["name"] = Name;
            j["url"] = Url;
            return j;
        }
    }
    public class InventoryItem
    {
        public int Id { get; set; }
        public string InventoryId { get; set; }

        [ForeignKey("ProductId")]
        public Product Product { get; set; }
        public string ProductId { get; set; }

        public DateTime AddedAt { get; set; }   
        public DateTime ExpiresAt { get; set; }

        [NotMapped]
        public bool HasExpired => ExpiresAt > DateTime.UtcNow;
    }
    public class HistoricItem
    {
        public int Id { get; set; }

        public string ProductId { get; set; }
        public string InventoryId { get; set; }

        public DateTime AddedAt { get; set; }
        public DateTime RemovedAt { get; set; }
    }
}
