﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;

namespace DiscordBot.Services
{
    public class FoodService : SavedService
    {
        public ConcurrentDictionary<string, List<string>> Manufacturers { get; set; } = new ConcurrentDictionary<string, List<string>>();
        public ConcurrentBag<SavedRecipe> Recipes { get; set; } = new ConcurrentBag<SavedRecipe>();

        public ConcurrentDictionary<int, WorkingRecipe> OngoingRecipes { get; set; } = new ConcurrentDictionary<int, WorkingRecipe>();
        
        public FoodDbContext DB()
        {
            return Program.Services.GetRequiredService<FoodDbContext>();
        }

        public Product GetProduct(string id)
        {
            using var db = DB();
            return db.GetProduct(id);
        }
        public InventoryItem GetInventoryItem(int id)
        {
            using var db = DB();
            return db.GetInventoryItem(id);
        }
        public Product AddProduct(string id, string name, string url, int? extends, string tags)
        {
            using var db = DB();
            return db.AddProduct(id, name, url, extends, tags);
        }
        public InventoryItem AddInventoryItem(string productId, string inventryId, DateTime expires, bool frozen)
        {
            using var db = DB();
            return db.AddInventoryItem(productId, inventryId, expires, frozen);
        }
        public List<InventoryItem> GetInventory(string id)
        {
            using var db = DB();
            return db.GetInventory(id);
        }
        public bool DeleteInventoryItem(int id)
        {
            using var db = DB();
            var item = GetInventoryItem(id);
            if (item == null)
                return false;
            db.Inventory.Remove(item);
            var his = new HistoricItem()
            {
                ProductId = item.ProductId,
                InventoryId = item.Id,
                AddedAt = item.AddedAt,
                RemovedAt = DateTime.Now.ToUniversalTime(),
            };
            db.PreviousInventory.Add(his);
            db.SaveChanges();
            return true;
        }

        public string GetManufacturor(string id)
        {
            foreach ((var key, var ls) in Manufacturers)
            {
                foreach (var x in ls)
                {
                    if (x.Length == id.Length)
                    {
                        if (id.StartsWith(x.Replace("_", "")))
                            return key;
                    }
                }
            }
            return null;
        }
        public void AddManufactorer(string name, string idPrefix)
        {
            if (Manufacturers.TryGetValue(name, out var ls))
                ls.Add(idPrefix);
            else
                Manufacturers[name] = new List<string>() { idPrefix };
            OnSave();
        }

        public override string GenerateSave()
        {
            var dict = new Dictionary<string, List<string>>(Manufacturers);
            var recipList = new List<SavedRecipe>(Recipes);
            var sv = new foodSave()
            {
                manufacturerPrefixes = dict,
                recipes = recipList
            };
            return Program.Serialise(sv);
        }
        public override void OnReady()
        {
            var sv = Program.Deserialise<foodSave>(ReadSave());
            Manufacturers = new ConcurrentDictionary<string, List<string>>(sv.manufacturerPrefixes ?? new Dictionary<string, List<string>>());
            Recipes = new ConcurrentBag<SavedRecipe>(sv.recipes ?? new List<SavedRecipe>());

        }
    }

    public class foodSave
    {
        [JsonProperty("manuf")]
        public Dictionary<string, List<string>> manufacturerPrefixes { get; set; } = new Dictionary<string, List<string>>();

        public List<SavedRecipe> recipes { get; set; } = new List<SavedRecipe>();

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
        public InventoryItem GetInventoryItem(int id)
        {
            return Inventory.Find(id);
        }
        public IReadOnlyCollection<InventoryItem> GetExpiresBetween(DateTime start, DateTime end)
        {
            return Inventory.AsEnumerable()
                .Where(x => x.ExpiresAt >= start && x.ExpiresAt < end)
                .ToArray();
        }
        public Product AddProduct(string id, string name, string url, int? extends, string tags)
        {
            var prod = new Product() {
                Id = id,
                Name = name,
                Url = url,
                FreezingExtends = extends,
                Tags = tags
            };
            var x = Products.Add(prod);
            SaveChanges();
            return x.Entity;
        }

        public InventoryItem AddInventoryItem(string productId, string inventoryId, DateTime expires, bool frozen)
        {
            var inv = new InventoryItem()
            {
                ProductId = productId,
                InventoryId = inventoryId,
                AddedAt = DateTime.UtcNow,
                InitialExpiresAt = expires,
                Frozen = frozen
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
            var x = CharSet.Utf8Mb4;
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
        public int? FreezingExtends { get; set; }
        public string Tags { get; set; }

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

        [Column("ExpiresAt")]
        public DateTime InitialExpiresAt { get; set; }
        public bool Frozen { get; set; }

        [NotMapped]
        public DateTime ExpiresAt => Frozen ? InitialExpiresAt.AddDays(Product?.FreezingExtends ?? 0) : InitialExpiresAt;
        [NotMapped]
        public bool HasExpired => ExpiresAt > DateTime.UtcNow;
    }
    public class HistoricItem
    {
        public int Id { get; set; }

        public string ProductId { get; set; }
        public int InventoryId { get; set; }

        public DateTime AddedAt { get; set; }
        public DateTime RemovedAt { get; set; }
    }

    public class SavedRecipe
    {
        static int _id = 0;
        public SavedRecipe()
        {
            Id = System.Threading.Interlocked.Increment(ref _id);
        }
        [JsonConstructor]
        private SavedRecipe(int id)
        {
            Id = id;
            if (id > _id)
                _id = id;
        }

        public int Id { get; }
        public Dictionary<string, int> Ingredients { get; set; } = new Dictionary<string, int>();
        [JsonProperty("steps", ItemTypeNameHandling = TypeNameHandling.All)]
        public List<SavedStep> Steps { get; set; } = new List<SavedStep>();

        [JsonProperty("order")]
        [DefaultValue(false)]
        public bool InOrder { get; set; }

        public WorkingRecipe ToWorking()
        {
            var multi = new WorkingMultiStep("Root", InOrder);
            foreach (var x in Steps.Select(x => x.ToWorking(multi)))
                multi.WithChild(x);
            multi.SetIdealTimeDiff(TimeSpan.Zero);
            return new WorkingRecipe(new[] {this})
            {
                Steps = multi
            };
        }
    }

    public class SavedStep
    {
        [JsonProperty("desc")]
        public string Description { get; set; }
        [JsonProperty("dura")]
        public int? Duration { get; set; }
        [JsonProperty("delay")]
        public int? Delay { get; set; }
        public List<SavedStep> Children { get; set; }

        [JsonProperty("order", NullValueHandling = NullValueHandling.Ignore)]
        [DefaultValue(true)]
        public bool? InOrder { get; set; }

        public WorkingStepBase ToWorking(WorkingMultiStep parent)
        {
            if(Children != null && Children.Count > 0)
            {
                var multi = new WorkingMultiStep(Description, InOrder.GetValueOrDefault(true));
                foreach (var x in Children)
                    multi.WithChild(x.ToWorking(multi));
                return multi;
            } else
            {
                return new WorkingSimpleStep(Description, Duration ?? 0, Delay ?? 0);
            }
        }



        public Classes.HTMLHelpers.Objects.ListItem GetListItem()
        {
            var li = new Classes.HTMLHelpers.Objects.ListItem(null);
            li.Children.Add(new Classes.HTMLHelpers.Objects.Span() { RawText = Description });

            if(Children == null || Children.Count == 0)
            {
                if (Duration.GetValueOrDefault(0) > 0)
                    li.Children.Add(new Classes.HTMLHelpers.Objects.Span() { RawText = $", for {Duration}" });
                if (Delay.GetValueOrDefault(0) > 0)
                    li.Children.Add(new Classes.HTMLHelpers.Objects.Span() { RawText = $", after {Delay}" });
            } else
            {
                var inner = new Classes.HTMLHelpers.Objects.UnorderedList();
                foreach (var x in Children ?? new List<SavedStep>())
                    inner.AddItem(x.GetListItem());
                li.Children.Add(inner);
            }
            return li;
        }
    }

    public class WorkingRecipe
    {
        static int _id = 1;
        public WorkingRecipe(SavedRecipe[] from)
        {
            Id = System.Threading.Interlocked.Increment(ref _id);
            From = from;
        }
        public int Id { get; }

        public bool Started { get; set; }

        public SavedRecipe[] From { get; }
        public DateTime? EstimatedEndAt
        {
            get
            {
                return DateTime.Now.AddSeconds(Steps.FullLength);
            }
        }

        public WorkingMultiStep Steps { get; set; }

        public WorkingStepBase OnScreenNow { get; set; }

    }

    public abstract class WorkingStepBase
    {
        public virtual string Description { get; protected set; }
        public virtual int Duration { get; protected set; }

        public virtual int Delay { get; protected set; }

        public TimeSpan IdealTimeDiff { get; set; }
        public abstract TimeSpan SetIdealTimeDiff(TimeSpan startDiff);

        public virtual DateTime? StartedAt { get; internal set; }

        public virtual DateTime? EndsAt
        {
            get
            {
                if (StartedAt.HasValue)
                    return StartedAt.Value.AddSeconds(FullLength);
                return null;
            }
        }

        public virtual int FullLength
        {
            get
            {
                int length = Duration + Delay;
                if (StartedAt.HasValue)
                {
                    var done = (DateTime.Now - StartedAt.Value).TotalSeconds;
                    return Math.Min(0, (int)Math.Round(length - done));
                }
                else if (IsFinished)
                {
                    return 0;
                }
                else
                {
                    return length;
                }
            }
        }

        public virtual DateTime? ActuallyEndedAt { get; private set; }

        public virtual bool IsFinished { get
            {
                return ActuallyEndedAt.HasValue;
            }  set
            {
                if (value)
                    ActuallyEndedAt = DateTime.Now;
                else
                    ActuallyEndedAt = null;
            }
        }

        public WorkingMultiStep Parent { get; set; }

        public string dbg { get
            {
                return "  " + (Parent?.dbg ?? "");
            } }
        public string dbg_text { get
            {
                if (Parent == null)
                    return Description;
                return Parent.dbg_text + " " + Description;
            } }

        public DateTime EstimatedStartTime { get
            {
                WorkingMultiStep parent = Parent;
                if (parent == null)
                    return StartedAt.GetValueOrDefault(DateTime.Now) + IdealTimeDiff;
                while (parent.Parent != null)
                    parent = parent.Parent;
                return parent.StartedAt.GetValueOrDefault(DateTime.Now) + IdealTimeDiff;
            } }

        public abstract void MarkStarted();
        public abstract void MarkDone();

        public abstract string GetDebuggerDisplay();

        public virtual JObject ToShortJson()
        {
            var jobj = new JObject();
            jobj["description"] = Description;
            jobj["at"] = new DateTimeOffset(EstimatedStartTime).ToUnixTimeMilliseconds().ToString();
            return jobj;
        }
    }

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class WorkingSimpleStep : WorkingStepBase
    {
        public WorkingSimpleStep(string desc, int dur, int del)
        {
            Description = desc;
            Duration = dur;
            Delay = del;
            StartedAt = null;
        }

        public override void MarkStarted()
        {
            Program.LogInfo($"{dbg}{dbg_text} Started: {Duration}-{Delay}", "SimpleStep");
            StartedAt = DateTime.Now;
        }

        public override string GetDebuggerDisplay()
        {
            return $"{Description} for {Duration}s taking {Delay}s";
        }

        public override void MarkDone()
        {
            Program.LogInfo($"{dbg}{dbg_text} Done: {Duration}-{Delay}", "SimpleStep");
            IsFinished = true;
        }

        public override TimeSpan SetIdealTimeDiff(TimeSpan startDiff)
        {
            IdealTimeDiff = startDiff;
            return startDiff + TimeSpan.FromSeconds(FullLength);
        }
    }

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class WorkingMultiStep : WorkingStepBase
    {
        public string Title { get; set; }
        public List<WorkingStepBase> Children { get; private set; } = new List<WorkingStepBase>();
        public int Step { get; set; }

        public bool InOrder { get; set; }

        public WorkingStepBase Previous => Children.ElementAtOrDefault(Step - 1);
        public WorkingStepBase Current => Children.ElementAtOrDefault(Step);

        public WorkingStepBase Next { get
            {
                if (Current is WorkingMultiStep && !Current.IsFinished)
                    return Current;
                return Children.ElementAtOrDefault(Step + 1);
            } }

        public WorkingMultiStep(string title, bool inOrder)
        {
            InOrder = inOrder;
            Title = title;
            Children = new List<WorkingStepBase>();
            Step = 0;
        }
        public WorkingMultiStep WithChild(WorkingStepBase c)
        {
            c.Parent = this;
            Children.Add(c);
            return this;
        }
        public override TimeSpan SetIdealTimeDiff(TimeSpan startDiff)
        {
            Sort();
            if(InOrder)
            {
                int length = 0;
                foreach (var step in Children)
                {
                    step.SetIdealTimeDiff(startDiff + TimeSpan.FromSeconds(length));
                    length += step.FullLength;
                }
                return startDiff + TimeSpan.FromSeconds(length);
            } else
            {
                var longest = Children.First();
                var maxLength = longest.FullLength;
                foreach (var step in Children)
                {
                    step.IdealTimeDiff = startDiff + TimeSpan.FromSeconds(maxLength - step.FullLength);
                }
                return startDiff + TimeSpan.FromSeconds(maxLength);
            }
        }
        public void Sort()
        {
            if (!InOrder) 
                Children = Children.OrderByDescending(x => x.FullLength).ToList();
            foreach (var x in Children)
            {
                if (x is WorkingMultiStep m)
                    m.Sort();
            }
        }

        public override string Description => Current.Description;
        public override int Delay => Current.Delay;
        public override DateTime? ActuallyEndedAt => Current?.ActuallyEndedAt;
        public override int Duration => Current.Duration;
        public override DateTime? StartedAt => Current?.StartedAt;
        public override DateTime? EndsAt => Current?.EndsAt;
        public override bool IsFinished => Step == Children.Count;

        public override int FullLength { get
            {
                if (IsFinished) return 0;
                if(InOrder)
                {
                    return Children.Sum(x => x.FullLength);
                } else
                {
                    return Children.OrderByDescending(x => x.FullLength).FirstOrDefault()?.FullLength ?? 0;
                }
            } }

        public override string GetDebuggerDisplay()
        {
            return $"[{Step}/{Children.Count}|{FullLength}] {(Current?.GetDebuggerDisplay() ?? "non")}";
        }

        public override void MarkStarted()
        {
            Program.LogInfo($"{dbg}{dbg_text} Started: {Current.Description} | {Step}/{Children.Count}", "MultiStep");
            Current.MarkStarted();
            Previous?.MarkDone();
            if (Current is WorkingSimpleStep s)
                Step++;
            else if (Current is WorkingMultiStep m)
                if (m.IsFinished)
                    Step++;
            Program.LogInfo($"{dbg}{dbg_text} Step now: {Step}/{Children.Count}", "MultiStep");
        }
        public override void MarkDone()
        {
            Program.LogInfo($"{dbg}{dbg_text} Done:?? {Step}", "MultiStep");
        }


    }
}
