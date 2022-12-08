using Discord;
using DiscordBot.Classes.Converters;
using DiscordBot.Utils;
using JsonSubTypes;
using Microsoft.EntityFrameworkCore;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class FoodService : SavedService
    {
        public ConcurrentDictionary<string, List<string>> Manufacturers { get; set; } = new ConcurrentDictionary<string, List<string>>();
        public ConcurrentBag<SavedRecipe> Recipes { get; set; } = new ConcurrentBag<SavedRecipe>();


        public ConcurrentDictionary<int, SavedMenu> Menus { get; set; } = new();

        public WorkingMenu WorkingMenu { get; set; }


        public ConcurrentDictionary<int, WorkingRecipe> OngoingRecipes { get; set; } = new ConcurrentDictionary<int, WorkingRecipe>();

        public ConcurrentDictionary<ulong, CancellationTokenSource> NotifyCancels { get; set; } = new();


        public const string DefaultInventoryId = "default";

        public FoodDbContext DB()
        {
            return Program.Services.GetRequiredService<FoodDbContext>();
        }

        public Product GetProduct(string id)
        {
            using var db = DB();
            return db.GetProduct(id);
        }
        public Product[] GetAllProducts()
        {
            using var db = DB();
            return db.Products.ToArray();
        }
        public InventoryItem GetInventoryItem(int id)
        {
            using var db = DB();
            return db.GetInventoryItem(id);
        }
        public Product AddProduct(string id, string name, string url, int? extends, int uses, string tags)
        {
            Product prod;
            using (var db = DB())
            {
                prod = db.AddProduct(id, name, url, extends, uses, tags);
            }
            var channel = getLogChannel().Result;
            var embed = new EmbedBuilder();
            embed.Title = $"New Product Added";
            var sb = new StringBuilder();
            var man = GetManufacturor(id);
            if(man != null)
                sb.AppendLine("**" + man + "**");
            sb.AppendLine(name);
            sb.AppendLine($"*{id}*");
            if(extends.HasValue)
                sb.AppendLine($"+{extends}");
            if (uses > 1)
                sb.AppendLine($"{uses} uses");
            if (!string.IsNullOrWhiteSpace(tags))
                sb.AppendLine(tags);
            if (!string.IsNullOrWhiteSpace(url))
                sb.AppendLine(url);
            embed.Description = sb.ToString();

            var components = new ComponentBuilder()
                .WithButton("Edit", $"food:edit:{id}", ButtonStyle.Primary);

            channel.SendMessageAsync(embed: embed.Build(), components: components.Build()).Wait();
            return prod;
        }
        public InventoryItem AddInventoryItem(string productId, string inventryId, DateTime expires, bool frozen, int? itemId = null)
        {
            using var db = DB();
            return db.AddInventoryItem(productId, inventryId, expires, frozen, itemId);
        }
        public List<InventoryItem> GetInventoryItems(string id)
        {
            using var db = DB();
            return db.GetInventory(id);
        }

        public List<HistoricItem> GetHistoricItems()
        {
            using var db = DB();
            return db.GetHistoricItems();
        }

        public HistoricItem GetHistoricItem(int? histId = null, int? invId = null)
        {
            if (histId == null && invId == null)
                throw new ArgumentNullException($"One of {nameof(histId)} and {nameof(invId)} must be present");
            using var db = DB();
            return db.GetHistoricItem(histId, invId);
        }

        public void RemoveHistoricItem(int id)
        {
            using var db = DB();
            db.RemoveHistoricItem(id);
        }
        
        public bool AddUsesInventoryItem(int id, int uses, DateTime backdateTo)
        {
            using var db = DB();
            var item = db.GetInventoryItem(id);
            if (item == null) return false;
            item.TimesUsed += uses;
            if(item.TimesUsed >= item.Product.Uses)
            {
                return DeleteInventoryItem(id, backdateTo, db);
            } else
            {
                db.Inventory.Update(item);
                db.SaveChanges();
                return true;
            }
        }
        public bool DeleteInventoryItem(int id, DateTime? deletedOn = null, FoodDbContext db = null)
        {
            bool hadDb = db != null;
            db ??= DB();
            try
            {
                var item = db.GetInventoryItem(id);
                if (item == null)
                    return false;
                db.Inventory.Remove(item);
                var his = new HistoricItem()
                {
                    ProductId = item.ProductId,
                    InventoryId = item.Id,
                    AddedAt = item.AddedAt,
                    RemovedAt = deletedOn ?? DateTime.UtcNow,
                };
                db.PreviousInventory.Add(his);
                db.SaveChanges();
                return true;
            }
            finally
            {
                if (!hadDb) db?.Dispose();
            }
        }
        public string GetManufacturor(string id)
        {
            if(id == null) return null;
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

        public WorkingRecipe ToWorkingRecipe(List<SavedRecipe> recipes, Dictionary<int, int> offsetDict, string title = null)
        {
            WorkingRecipe working;
            if (recipes.Count == 1)
            {
                working = recipes[0].ToWorking();
            }
            else
            {
                working = new WorkingRecipe(recipes.ToArray());
                var combined = new WorkingMultiStep(null, title ?? "Combined Root", false);
                foreach (var x in recipes)
                    combined.WithChild(x.ToChild());
                combined.SetIdealTimeDiff(TimeSpan.Zero, null);

                foreach (var step in combined.Children)
                {
                    var offset = offsetDict.GetValueOrDefault(step.Recipe.Id, 0);
                    if (offset != 0)
                        step.OffsetIdealTimeDiff(TimeSpan.FromSeconds(offset));
                }

                var flattened = combined.Flatten();
                working.WithSteps(flattened);
            }
            OngoingRecipes.TryAdd(working.Id, working);
            return working;
        }
        public WorkingRecipe ToWorkingRecipe(SavedRecipe recipe, Dictionary<int, int> offsetDict)
        {
            if(recipe.Children != null && recipe.Children.Count > 0)
            {
                var children = recipe.Children.Select(keypair =>
                {
                    return Recipes.FirstOrDefault(x => x.Id == keypair.Key);
                }).ToList();
                return ToWorkingRecipe(children, recipe.Children, recipe.Title);
            } else
            {
                return ToWorkingRecipe(new List<SavedRecipe> { recipe }, offsetDict, recipe.Title);
            }
        }

        public void DeleteRecipe(int id)
        {
            var ls = new List<SavedRecipe>();
            foreach(var x in Recipes)
                if(x.Id != id)
                    ls.Add(x);
            Recipes = new ConcurrentBag<SavedRecipe>(ls);
        }

        async Task<IMessageChannel> getLogChannel()
        {
            IMessageChannel channel;
            if (ulong.TryParse(Program.Configuration["settings:foodnotify"], out var channelId))
            {
                channel = Program.Client.GetChannel(channelId) as IMessageChannel;
            }
            else
            {
                channel = await Program.AppInfo.Owner.CreateDMChannelAsync();
            }
            return channel;
        }

        public async Task NotifyScannedProduct(string code)
        {
            EmbedBuilder builder = new();
            ComponentBuilder components = null;
            builder.Title = "Scanned";
            string link = $"{MLAPI.Handler.LocalAPIUrl}/food/new?code={code}";
            builder.Url = link;
            var desc = new StringBuilder();
            desc.Append($"**{code}**");
            var product = GetProduct(code);
            if (product == null)
            {
                desc.Append("\nNo product exists with that code.\n" +
                    $"Add one [at this link]({link})");
                builder.Color = Color.Red;
            } else
            {
                var manu = this.GetManufacturor(code);
                desc.Append("\n");
                if (manu != null)
                    desc.Append($"[{manu}] ");
                desc.Append(product.Name);
                var items = product.InventoryItems ?? new List<InventoryItem>();
                if(items.Count == 0)
                {
                    desc.Append("\nThere are no existing items of this product.");
                } else
                {
                    desc.Append($"\nSelect an item below to remove it.");
                    var sm = new SelectMenuBuilder();
                    sm.WithCustomId($"food:scanned:{code}");
                    sm.MaxValues = 1;
                    foreach (var item in items)
                        sm.AddOption($"Added {item.AddedAt:yyyy-MM-dd}, expires {item.InitialExpiresAt:yyyy-MM-dd}{(item.Frozen ? " (Frozen)" : "")}", item.Id.ToString());
                    components = new ComponentBuilder()
                        .WithSelectMenu(sm);
                }
            }
            builder.Description = desc.ToString();
            IMessageChannel channel = await getLogChannel();
            await channel.SendMessageAsync(embeds: new[] { builder.Build() }, components: components?.Build() ?? null);
        }

        EmbedBuilder toEmbed(WorkingMenuDay day, bool today, out bool anyFrozen) 
        {
            var builder = new EmbedBuilder();
            builder.Title = today ? "Menu" : "Reminder";
            builder.Color = today ? Color.Green : Color.Red;

            var keys = day.Items.Keys.ToList();
            keys.AddRange(day.Text.Keys);
            anyFrozen = false;
            foreach(var key in keys.Distinct())
            {
                var v = new StringBuilder();
                if (day.Text.TryGetValue(key, out var t))
                    v.AppendLine($"**{t}**");
                if(day.Items.TryGetValue(key, out var items))
                {
                    foreach(var item in items)
                    {
                        v.AppendLine(item.Describe(this));
                        if (item.Item.Frozen)
                            anyFrozen = true;
                    }
                }
                if (v.Length == 0)
                    v.Append("-");
                builder.AddField(key, v, true);
            }
            return builder;
        }

        public EmbedBuilder getYesterdayUsedItems()
        {
            var yes = DateTime.UtcNow.AddDays(-1);
            var yesterday = WorkingMenu.Days.FirstOrDefault(x => x.Date.IsSameDay(yes));
            if (yesterday != null)
            {
                var usesEmbed = new EmbedBuilder();
                usesEmbed.Title = "Confirm Uses";
                usesEmbed.Description = "Please confirm that the following items were used yesterday.";
                using var db = DB();
                foreach ((var group, var itemsLs) in yesterday.Items)
                {
                    foreach (var item in itemsLs)
                    {
                        if (item == null || item.Item == null) continue;

                        var confirmExists = db.GetInventoryItem(item.Item.Id);
                        if(confirmExists == null)
                        {
                            Info($"Skipping {item.Item.Describe(this)} because it does not exist in the inventory.");
                            continue;
                        }

                        usesEmbed.AddField(item.Item.Id.ToString(), $"{item.Uses}x {item.Item.Describe(this)}");
                    }

                }
                if (usesEmbed.Fields.Count == 0) return null;

                return usesEmbed;
            }
            return null;
        }

        [Cron("6,18", "0")]
        public void SendMenuNotifs(int hour)
        {
            if (WorkingMenu == null) return;


            var embeds = new List<Embed>();
            MessageComponent components = null;
            var now = DateTime.UtcNow;
            var mention = false;
            if(hour < 12)
            {
                // morning reminder
                var today = WorkingMenu.Days.FirstOrDefault(x => x.Date.IsSameDay(now));
                embeds.Add(toEmbed(today, true, out mention).Build());
            } else
            {
                now = now.AddDays(1);
                WorkingMenuDay tomorrow;
                tomorrow = WorkingMenu.Days.FirstOrDefault(x => x.Date.IsSameDay(now));
                if (tomorrow == null)
                {
                    attemptFullfill(false);
                    tomorrow = WorkingMenu.Days.FirstOrDefault(x => x.Date.IsSameDay(now));
                    if(tomorrow == null)
                    {
                        Error("Tomorrow was still null even after adding new menu days?");
                        var sb = new StringBuilder();
                        sb.Append($"Days are:\n");
                        foreach(var x in WorkingMenu.Days)
                        {
                            sb.AppendLine($"- {x.Date}");
                        }
                        Error(sb.ToString());
                        return;
                    }
                }
                embeds.Add(toEmbed(tomorrow, false, out mention).Build());
                if(mention) // there's frozen stuff
                {
                    components = new ComponentBuilder().WithButton("Defrosted", "food:notify:defrost", ButtonStyle.Danger).Build();
                }
            }

            var lc = getLogChannel().Result;
            var msg = lc.SendMessageAsync(text: (mention ? "@everyone" : null), embeds: embeds.ToArray(), components: components).Result;
            if(mention && components != null)
            {
                var src = new CancellationTokenSource();
                NotifyCancels[msg.Id] = src;
                Task.Run(async () =>
                {
                    while(!src.Token.IsCancellationRequested)
                    {
                        await Task.Delay(Time.Ms.Minute * 5, src.Token);
                        await lc.SendMessageAsync("Defrost. @everyone");
                    }
                });
            }
        }

        void attemptFullfill(bool forceLog)
        {
            Menus.TryGetValue(WorkingMenu.NextComingUp, out var savedMenu);
            savedMenu.Fulfill(this, DefaultInventoryId, DateTime.UtcNow.NextDay(DayOfWeek.Monday).Date, forceLog);
        }

        void doFreezerChecks()
        {
            attemptFullfill(DateTime.UtcNow.DayOfWeek == DayOfWeek.Sunday);
            using var db = DB();
            var inventory = db.GetInventory(DefaultInventoryId);
            var usedOnDate = WorkingMenu.GetItemsUsed();

            var nextDate = DateTime.UtcNow.Date.AddDays(8);
            var embed = new EmbedBuilder();
            embed.Title = "Needs Freezing";
            embed.Color = Color.Blue;
            var sb = new StringBuilder();
            foreach(var item in inventory) 
            {
                if (item.Frozen) continue;
                if(item.Product != null)
                {
                    if (!item.Product.FreezingExtends.HasValue) continue; // can't be frozen
                }
                var assumeUsedOn = usedOnDate.GetValueOrDefault(item.Id, nextDate);
                if(item.ExpiresAt < assumeUsedOn) 
                {
                    sb.AppendLine(item.Describe(this));
                }
            }

            if(sb.Length > 0)
            {
                embed.Description = sb.ToString();
                var lc = getLogChannel().Result;
                lc.SendMessageAsync(text: "@everyone", embed: embed.Build()).Wait();
            }
        }

        public async void DoMenuChecks()
        {
            if (WorkingMenu == null) return;
            // set uses on yesterday's stuff
            var yesE = getYesterdayUsedItems();
            if (yesE != null)
            {
                var lc = await getLogChannel();
                var components = new ComponentBuilder()
                    .WithButton("Confirm", "food:uses:confirm", ButtonStyle.Success)
                    .WithButton("Refresh", "food:uses:refresh", ButtonStyle.Secondary)
                    .Build();
                await lc.SendMessageAsync(embed: yesE.Build(), components: components);
            }

            doFreezerChecks();
            OnSave();
        }



        public override string GenerateSave()
        {
            var dict = new Dictionary<string, List<string>>(Manufacturers);
            var recipList = new List<SavedRecipe>(Recipes);
            var sv = new foodSave()
            {
                manufacturerPrefixes = dict,
                recipes = recipList,
                menus = Menus.Values.ToList(),
                curMenu = WorkingMenu
            };
            return Program.Serialise(sv, conv: new InventoryItemConverter());
        }
        
        
        
        public override void OnReady()
        {
            var sv = Program.Deserialise<foodSave>(ReadSave(), new InventoryItemConverter());
            Manufacturers = new ConcurrentDictionary<string, List<string>>(sv.manufacturerPrefixes ?? new Dictionary<string, List<string>>());
            Recipes = new ConcurrentBag<SavedRecipe>(sv.recipes ?? new List<SavedRecipe>());
            Menus = new ConcurrentDictionary<int, SavedMenu>();
            foreach (var x in sv.menus)
                Menus[x.Id] = x;
            WorkingMenu = sv.curMenu;
            if(WorkingMenu != null)
            {
                foreach (var day in WorkingMenu.Days)
                {
                    foreach ((var key, var ls) in day.Items)
                    {
                        ls.RemoveAll(x => x == null || x.Item == null);
                    }
                }
            }
#if DEBUG
#endif

        }

        public override void OnDailyTick()
        {
            DoMenuChecks();
        }

    }

    public class foodSave
    {
        [JsonProperty("manuf")]
        public Dictionary<string, List<string>> manufacturerPrefixes { get; set; } = new Dictionary<string, List<string>>();

        public List<SavedRecipe> recipes { get; set; } = new List<SavedRecipe>();

        public List<SavedMenu> menus { get; set; } = new();

        public WorkingMenu curMenu { get; set; }
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
        public Product AddProduct(string id, string name, string url, int? extends, int uses, string tags)
        {
            var prod = new Product() {
                Id = id,
                Name = name,
                Url = url,
                FreezingExtends = extends,
                Tags = tags,
                Uses = uses
            };
            var x = Products.Add(prod);
            SaveChanges();
            return x.Entity;
        }

        public InventoryItem AddInventoryItem(string productId, string inventoryId, DateTime expires, bool frozen, int? itemId = null)
        {
            var inv = new InventoryItem()
            {
                ProductId = productId,
                InventoryId = inventoryId,
                AddedAt = DateTime.UtcNow,
                InitialExpiresAt = expires,
                Frozen = frozen,
                TimesUsed = 0
            };
            if (itemId.HasValue)
                inv.Id = itemId.Value;
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

        public List<HistoricItem> GetHistoricItems()
        {
            return PreviousInventory
                .AsQueryable()
                .ToList();
        }

        public HistoricItem GetHistoricItem(int? histId, int? invId)
        {
            if(histId.HasValue)
                return PreviousInventory.FirstOrDefault(x => x.Id == histId.Value);
            return PreviousInventory.FirstOrDefault(x => x.InventoryId == invId.Value);
        }
        public void RemoveHistoricItem(int histId)
        {
            var f = PreviousInventory.Find(histId);
            if (f == null) return;
            PreviousInventory.Remove(f);
            SaveChanges();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
#if WINDOWS
            options.UseSqlServer(Program.getDbString("food"));
            var x = CharSet.Utf8Mb4;
#else
                options.UseMySql(Program.getDbString("food"),
                    new MariaDbServerVersion(new Version(10, 3, 25)));
#endif
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasCharSet("utf8mb4");

            modelBuilder.Entity<Product>()
                .HasKey(x => x.Id);
            modelBuilder.Entity<Product>()
                .HasMany(x => x.InventoryItems)
                .WithOne(x => x.Product)
                .HasForeignKey(x => x.ProductId);
            modelBuilder.Entity<Product>()
                .Navigation(x => x.InventoryItems)
                .AutoInclude();
            modelBuilder.Entity<Product>()
                .Property(x => x.Uses)
                .HasDefaultValue(1);

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

        public int Uses { get; set; }

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
        public virtual string InventoryId { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
        public virtual string ProductId { get; set; }

        public virtual DateTime AddedAt { get; set; }

        [Column("ExpiresAt")]
        public virtual DateTime InitialExpiresAt { get; set; }
        public virtual bool Frozen { get; set; }

        public virtual int TimesUsed { get; set; }

        [NotMapped]
        public DateTime ExpiresAt => Frozen ? InitialExpiresAt.AddDays(Product?.FreezingExtends ?? 0) : InitialExpiresAt;
        [NotMapped]
        public bool HasExpired => ExpiresAt > DateTime.UtcNow;

        public string Describe(FoodService serv = null)
        {
            serv ??= Program.Services.GetRequiredService<FoodService>();
            var v = new StringBuilder();
            var m = serv.GetManufacturor(ProductId);
            if (!string.IsNullOrWhiteSpace(m))
                v.Append($"({m}) ");
            v.Append($"{(Product?.Name ?? ProductId)} {InitialExpiresAt:yyyy-MM-dd} {(Frozen ? "(**Frozen**)" : "")}");
            return v.ToString();
        }
        public JObject ToJson(bool withManufacturer = false, FoodService srv = null)
        {
            var jobj = Product?.ToJson() ?? new JObject();
            jobj["product_id"] = ProductId;
            jobj["id"] = Id;
            jobj["added"] = new DateTimeOffset(AddedAt).ToUnixTimeMilliseconds();
            jobj["expires"] = new DateTimeOffset(ExpiresAt).ToUnixTimeMilliseconds();
            jobj["max_uses"] = Product?.Uses ?? 1;
            jobj["times_used"] = TimesUsed;
            if(Frozen)
                jobj["true_expires"] = new DateTimeOffset(InitialExpiresAt).ToUnixTimeMilliseconds();
            jobj["frozen"] = Frozen;
            if(withManufacturer)
            {
                srv ??= Program.Services.GetRequiredService<FoodService>();
                var m = srv.GetManufacturor(ProductId);
                jobj["manu"] = m;
            }
            return jobj;
        }
    }
    public class HistoricItem
    {
        public int Id { get; set; }

        public string ProductId { get; set; }
        public int InventoryId { get; set; }

        public DateTime AddedAt { get; set; }
        public DateTime RemovedAt { get; set; }
    }

    #region Menu stuff

    public class WorkingMenu
    {
        public WorkingMenu(string title)
        {
            Title = title;
        }

        public DateTime StartDate
        {
            get
            {
                return Days.FirstOrDefault()?.Date ?? DateTime.MinValue;
            }
        }
        public string Title { get; set; }
        public List<WorkingMenuDay> Days { get; set; } = new();

        public Dictionary<int, DateTime> GetItemsUsed()
        {
            var d = new Dictionary<int, DateTime>();
            foreach (var day in Days) {
                var items = day.GetItemsUsed();
                foreach(var item in items) 
                {
                    if(!d.ContainsKey(item.Id)) 
                    {
                        d[item.Id] = day.Date;
                    }
                }
            }
            return d;
        }

        struct orderData
        {
            public SavedMenuItem Item { get; set; }
            public string Group { get; set; }
            public WorkingMenuDay Day { get; set; }
        }
        public void AttemptToFill(FoodService service, string inventoryId, StringBuilder log, out int added)
        {
            added = 0;
            service.Menus.TryGetValue(NextComingUp, out var menu);
            using var db = service.DB();




            var _now = DateTime.UtcNow.Date;
            int removed = Days.RemoveAll(x => (_now - x.Date).TotalDays > 7);
            log.AppendLine($"- Removed {removed} old days.");
            var lastDay = Days.LastOrDefault()?.Date ?? _now;
            if((lastDay - _now).TotalDays < 7)
            {
                log.AppendLine($"- {lastDay} is too close, appending another week.");
                var mondate = lastDay.NextDay(DayOfWeek.Monday);
                for(int i = 0; i < 7; i++)
                {
                    var newDay = new WorkingMenuDay(mondate.Date, new());
                    Days.Add(newDay);
                    mondate = mondate.AddDays(1);
                }
            }
            var inventory = db.GetInventory(inventoryId);

            var alreadyUsed = GetItemsUsed();
            var usedRemoved = 0;
            foreach((var id, var day) in alreadyUsed)
            {
                usedRemoved += inventory.RemoveAll(x => x.Id == id);
            }

            log.AppendLine($"- Fetched {inventory.Count} items from the inventory {inventoryId}, excuding {usedRemoved} already present");
            log.AppendLine("");

            var items = new List<orderData>();
            foreach(var workingDay in Days)
            {
                var menuDay = menu.GetSavedDay(workingDay.Date.DayOfWeek);
                log.AppendLine($"= Day: {workingDay.Date}");
                if (workingDay.ManualOverride)
                {
                    log.AppendLine("  ** Manual mode. **");
                    continue;
                }
                workingDay.Text = menuDay.Text;
                foreach (var keypair in menuDay.Items)
                {
                    log.AppendLine($"  {keypair.Key}: ");
                    foreach (var item in keypair.Value)
                    {
                        log.AppendLine($"   - {item.Type}");
                        var d = new orderData()
                        {
                            Item = item,
                            Group = keypair.Key,
                            Day = workingDay
                        };
                        items.Add(d);
                    }
                }
            }
            items = items.OrderBy(x => x.Item.Priority).ToList();
            log.AppendLine($"");
            log.AppendLine($"Fulfilling: ");
            var itemUses = new Dictionary<int, int>();
            foreach (var data in items)
            {
                log.AppendLine($"{data.Day.Date} {data.Group} {data.Item.Type}: ");
                var validItems = data.Item.CollectValid(inventory);
                foreach (var item in validItems)
                    log.AppendLine($"  - {item.Id} {item.Product.Name} {item.ExpiresAt} {item.Frozen}");


                validItems = validItems.Where(item =>
                { // filter items that are too few to be used
                    var rem = (item.Product?.Uses ?? 0) - item.TimesUsed;
                    if (rem < data.Item.AmountUsed) return false;

                    return true;
                }).ToList();

                validItems = validItems.OrderBy((item) =>
                {   // prioritise:
                    // (0) items that will expire on or before this date.
                    // then
                    // (1) items that haven't been used this week.
                    // then
                    // (2) items that haven't been used in the last day.
                    if(item.ExpiresAt < data.Day.Date || item.ExpiresAt.IsSameDay(data.Day.Date))
                    {
                        return 0;
                    }
                    if(!itemUses.ContainsKey(item.Id))
                    {
                        return 1;
                    } else
                    { // item has been used to some degree.
                        return 2;
                    }
                }).ThenBy(x => x.ExpiresAt).ToList();

                var bestItem = validItems.FirstOrDefault();
                log.AppendLine($"Selected: {(bestItem?.Id.ToString() ?? "null")}");
                if (bestItem != null)
                {
                    var used = itemUses.GetValueOrDefault(bestItem.Id, bestItem.TimesUsed);
                    used += data.Item.AmountUsed;
                    if(used >= (bestItem.Product?.Uses ?? 1))
                    {
                        log.AppendLine(" > Item will be used.");
                        inventory.Remove(bestItem);
                    } else
                    {
                        log.AppendLine($" > Item will uses: {used}");
                        itemUses[bestItem.Id] = used;
                    }

                    var working = new WorkingMenuItem()
                    {
                        Item = bestItem,
                        Uses = data.Item.AmountUsed
                    };
                    data.Day.Items.AddInner(data.Group, working);
                    data.Day.ManualOverride = true; // since we've already automatically sorted it, we'll hand it over for manual use
                    added += 1;
                }
            }
        }


        public int FromMenu { get; set; }

        public int NextComingUp { get; set; }

        public string[] GetGroups()
        {
            var ls = new List<string>();
            foreach(var x in Days)
            {
                foreach(var key in x.Items.Keys)
                {
                    if (key != "*" && ls.Contains(key) == false) ls.Add(key);
                }
            }
            return ls.ToArray();
        }
    }
    public class WorkingMenuDay
    {
        public WorkingMenuDay(DateTime date, Dictionary<string, string> text)
        {
            Text = text;
            Date = date.Date;
        }
        public DateTime Date { get; set; }
        public Dictionary<string, string> Text { get; set; }
        public Dictionary<string, List<WorkingMenuItem>> Items { get; set; } = new();

        [JsonProperty("manual")]
        public bool ManualOverride { get; set; }

        public List<InventoryItem> GetItemsUsed()
        {
            var ls = new List<InventoryItem>();
            foreach(var keypair in Items)
            {
                foreach (var i in keypair.Value ?? new List<WorkingMenuItem>())
                    if(i != null && i.Item != null)
                        ls.Add(i.Item);
            }
            return ls.DistinctBy(x => x.Id).ToList();
        }
    
        
    }

    public class WorkingMenuItem
    {
        public InventoryItem Item { get; set; }
        public int Uses { get; set; }

        public string Describe(FoodService foodService)
        {
            var s = Item?.Describe(foodService) ?? "(null)";
            if (Uses > 1)
                return $"{Uses}x {s}";
            return s;
        }
    }

    public class SavedMenu
    {
        public string Title { get; set; }
        public List<SavedMenuDay> Days { get; set; } = new List<SavedMenuDay>();

        public int Id { get; set; }
        private static int _id;

        public SavedMenu()
        {
            Id = System.Threading.Interlocked.Increment(ref _id);
        }
        [JsonConstructor]
        public SavedMenu( int id )
        {
            Id = id;
            if (id > _id)
                _id = Id;
        }


        public void Fulfill(FoodService service, string inventoryId, DateTime? startdate, bool forceLog)
        {
            var log = new StringBuilder();

            log.AppendLine("== Starting menu conversion ==");
            var menu = service.WorkingMenu;
            if(menu == null)
            {
                menu = new WorkingMenu("");
                service.WorkingMenu = menu;
            }
            var added = 0;
            try
            {
                menu.Title = this.Title;    
                menu.FromMenu = this.Id;
                menu.NextComingUp = this.Id;
                menu.AttemptToFill(service, inventoryId, log, out added);
            } 
            finally
            {
                if(forceLog || added > 0)
                    Program.LogOwner(log);
            }
        }

        public SavedMenuDay GetSavedDay(DayOfWeek day)
        {
            switch(day)
            {
                case DayOfWeek.Monday:
                    return Days.ElementAtOrDefault(0);
                case DayOfWeek.Tuesday:
                    return Days.ElementAtOrDefault(1);  
                case DayOfWeek.Wednesday:
                    return Days.ElementAtOrDefault(2);
                case DayOfWeek.Thursday:
                    return Days.ElementAtOrDefault(3);
                case DayOfWeek.Friday:
                    return Days.ElementAtOrDefault(4);
                case DayOfWeek.Saturday:
                    return Days.ElementAtOrDefault(5);
                case DayOfWeek.Sunday:
                    return Days.ElementAtOrDefault(6);
                default: return null;
            }
        }

        public JObject ToJson()
        {
            var jobj = new JObject();
            jobj["title"] = Title;
            var jarr = new JArray();
            foreach(var day in Days)
            {
                jarr.Add(day.ToJson());
            }
            jobj["days"] = jarr;
            return jobj;
        }
    }

    public class SavedMenuDay
    {
        public Dictionary<string, string> Text { get; set; }

        public Dictionary<string, List<SavedMenuItem>> Items { get; set; } = new();

        public JObject ToJson()
        {
            var jobj = new JObject();

            var text = new JObject();
            foreach ((var k, var v) in Text)
                text[k] = v;
            jobj["text"] = text;

            var items = new JObject();
            foreach((var key, var val) in Items)
            {
                var arr = new JArray();
                foreach (var item in val)
                    arr.Add(item.ToJson());
                items[key] = arr;
            }
            jobj["items"] = items;


            return jobj;
        }
    }

    [JsonConverter(typeof(JsonSubtypes), "Type")]
    [JsonSubtypes.KnownSubType(typeof(SavedMenuIdItem), "Id")]
    [JsonSubtypes.KnownSubType(typeof(SavedMenuTagItem), "Tag")]
    public abstract class SavedMenuItem
    {
        public abstract string Type { get; }
        public int Priority { get; set; } = 0;

        public int AmountUsed { get; set; } = 1;
        public abstract List<InventoryItem> CollectValid(List<InventoryItem> items);

        public abstract JObject ToJson();
    }

    public class SavedMenuIdItem : SavedMenuItem
    {
        public SavedMenuIdItem()
        {
            Priority = 1;
        }
        public override string Type => "Id";

        public List<string> Ids { get; set; }

        public override List<InventoryItem> CollectValid(List<InventoryItem> items)
        {
            return items.Where(x => Ids.Contains(x.ProductId)).ToList();
        }

        public override JObject ToJson()
        {
            var j = new JObject();
            j["type"] = "id";
            j["value"] = JArray.FromObject(Ids);
            j["uses"] = this.AmountUsed;
            return j;
        }
    }

    public class SavedMenuTagItem : SavedMenuItem
    {
        public override string Type => "Tag";

        public List<string> Tags { get; set; }

        public override List<InventoryItem> CollectValid(List<InventoryItem> items)
        {
            var valid = new List<InventoryItem>();
            foreach(var inv in items)
            {
                if (string.IsNullOrWhiteSpace(inv.Product?.Tags ?? ""))
                    continue;
                foreach(var t in inv.Product?.Tags.Split(';'))
                {
                    if(Tags.Contains(t))
                    {
                        valid.Add(inv);
                        continue;
                    }
                }
            }
            return valid;
        }

        public override JObject ToJson()
        {
            var j = new JObject();
            j["type"] = "tag";
            j["value"] = JArray.FromObject(Tags);
            j["uses"] = this.AmountUsed;
            return j;
        }
    }

    #endregion

    #region Recipe stuff
    public class SavedIngredient
    {
        [JsonProperty("am")]
        public int Amount { get; set; }
        [JsonProperty("fr")]
        public bool Frozen { get; set; }

        public SavedIngredient(int amount, bool frozen)
        {
            Amount = amount;
            Frozen = frozen;
        }
    }

    public class SavedRecipe
    {
        static int _id = 0;
        public SavedRecipe(int id = -1)
        {
            Id = id == -1 ? System.Threading.Interlocked.Increment(ref _id) : id;
        }
        [JsonConstructor]
        private SavedRecipe(int id, string s_ = null)
        {
            Id = id;
            if (id > _id)
                _id = id;
        }

        [JsonProperty("title")]
        public string Title { get; set; }

        public int Id { get; }
        [JsonProperty("Ingredients", ItemConverterType = typeof(Classes.Converters.RecipeIngredientConverter))]
        public Dictionary<string, SavedIngredient> Ingredients { get; set; } = new Dictionary<string, SavedIngredient>();
        [JsonProperty("steps", ItemTypeNameHandling = TypeNameHandling.All)]
        public List<SavedStep> Steps { get; set; } = new List<SavedStep>();

        [JsonProperty("children")]
        public Dictionary<int, int> Children { get; set; } = new();

        [JsonProperty("order")]
        [DefaultValue(false)]
        public bool InOrder { get; set; }

        public int Kind { get
            {
                if (Children != null && Children.Count > 0) return 0;
                return 1;
            } }

        public WorkingMultiStep ToChild()
        {
            var multi = new WorkingMultiStep(this, $"Root {Id}", InOrder);
            foreach (var x in Steps.Select(x => x.ToWorking(multi)))
                multi.WithChild(x);
            return multi;
        }

        public WorkingRecipe ToWorking()
        {
            var multi = ToChild();
            multi.SetIdealTimeDiff(TimeSpan.Zero, null);

            return new WorkingRecipe(new[] { this })
                .WithSteps(multi.Flatten());
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
                var multi = new WorkingMultiStep(parent.Recipe, Description, InOrder.GetValueOrDefault(true));
                foreach (var x in Children)
                    multi.WithChild(x.ToWorking(multi));
                return multi;
            } else
            {
                return new WorkingSimpleStep(parent.Recipe, Description, Duration ?? 0, Delay ?? 0);
            }
        }

        string tostr(int? seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds.GetValueOrDefault(0));
            var sb = new StringBuilder();
            if (ts.Hours > 0)
                sb.Append($"{ts.Hours}h");
            if (ts.Minutes > 0)
                sb.Append($"{ts.Minutes}m");
            if (ts.Seconds > 0)
                sb.Append($"{ts.Seconds}s");

            if (sb.Length == 0)
                sb.Append("0");

            return sb.ToString();
        }

        public Classes.HTMLHelpers.Objects.ListItem GetListItem()
        {
            var li = new Classes.HTMLHelpers.Objects.ListItem(null);
            li.Children.Add(new Classes.HTMLHelpers.Objects.Span() { RawText = Description });

            if(Children == null || Children.Count == 0)
            {
                if (Duration.GetValueOrDefault(0) > 0)
                    li.Children.Add(new Classes.HTMLHelpers.Objects.Span() { RawText = $", for {tostr(Duration)}" });
                if (Delay.GetValueOrDefault(0) > 0)
                    li.Children.Add(new Classes.HTMLHelpers.Objects.Span() { RawText = $", after {tostr(Delay)}" });
            } else
            {

                var inner = (InOrder ?? true)  ? (Classes.HTMLHelpers.DOMBase) new Classes.HTMLHelpers.Objects.OrderedList() 
                                               : (Classes.HTMLHelpers.DOMBase) new Classes.HTMLHelpers.Objects.UnorderedList();
                foreach (var x in Children ?? new List<SavedStep>())
                    if(inner is Classes.HTMLHelpers.Objects.OrderedList ol)
                        ol.AddItem(x.GetListItem());
                    else if(inner is Classes.HTMLHelpers.Objects.UnorderedList ul)
                        ul.AddItem(x.GetListItem());
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

        public bool Started => Index > 0;

        public SavedRecipe[] From { get; }
        public DateTime? EstimatedEndAt
        {
            get
            {
                DateTime maxEnds = DateTime.Now;
                foreach(var s in Steps)
                {
                    var ends = s.StartedAt.GetValueOrDefault(s.EstimatedStartTime) + TimeSpan.FromSeconds(s.FullLength);
                    if(ends > maxEnds)
                    {
                        maxEnds = ends;
                    }
                }
                return maxEnds;
            }
        }

        public WorkingSimpleStep[] Steps { get; set; }

        public WorkingRecipe WithSteps(List<WorkingSimpleStep> flat)
        {
            flat = flat.OrderBy(x => x.IdealTimeDiff).ToList();
            WorkingSimpleStep collate = null;
            for (int i = 0; i < flat.Count; i++)
            {
                if (collate == null)
                {
                    collate = flat[i];
                    continue;
                }
                var next = flat[i];
                var diff = Math.Abs((collate.IdealTimeDiff - next.IdealTimeDiff).TotalSeconds);
                if (diff < 1)
                { // they're close enough to merge together
                    collate.Description += " & " + next.Description;
                    flat.RemoveAt(i);
                    i--;
                } else
                {
                    collate = next;
                }
            }
            Steps = flat.ToArray();
            return this;
        }

        public void UpdateEstimatedTimes()
        {
            var startedAt = Steps.First().StartedAt.GetValueOrDefault(DateTime.Now);
            TimeSpan lastDiff = TimeSpan.Zero;
            foreach(var step in Steps)
            {
                var diff = step.IdealTimeDiff - lastDiff;
                lastDiff = step.IdealTimeDiff;
                if(step.StartedAt.HasValue)
                {
                    step.EstimatedStartTime = step.StartedAt.Value;
                    startedAt = step.StartedAt.Value;
                } else
                {
                    step.EstimatedStartTime = startedAt + diff;
                    startedAt = step.EstimatedStartTime;
                }
            }
        }

        public int Index { get; set; }
        public WorkingSimpleStep Previous => Steps.ElementAtOrDefault(Index - 1);
        public WorkingSimpleStep OnScreenNow => Steps.ElementAtOrDefault(Index);
        public WorkingSimpleStep Next => Steps.ElementAtOrDefault(Index + 1);
    }

    public abstract class WorkingStepBase
    {
        public SavedRecipe Recipe { get; set; }
        public WorkingStepBase(SavedRecipe recipe)
        {
            Recipe = recipe;
        }
        public virtual string Description { get; set; }
        public virtual int Duration { get; protected set; }

        public virtual int Delay { get; protected set; }

        public TimeSpan IdealTimeDiff { get; set; }
        public abstract TimeSpan SetIdealTimeDiff(TimeSpan startDiff, TimeSpan? targetEnd);
        public abstract void OffsetIdealTimeDiff(TimeSpan offset);

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

        public DateTime EstimatedStartTime { get; set; }

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
        public virtual JObject ToFullJson()
        {
            var jobj = ToShortJson();
            jobj["diff"] = (int)IdealTimeDiff.TotalSeconds;
            jobj["duration"] = Duration + Delay;
            return jobj;
        }
    }

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class WorkingSimpleStep : WorkingStepBase
    {
        public WorkingSimpleStep(SavedRecipe recipe, string desc, int dur, int del) : base(recipe)
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

        public override TimeSpan SetIdealTimeDiff(TimeSpan startDiff, TimeSpan? targetEnd)
        {
            IdealTimeDiff = startDiff;
            return startDiff + TimeSpan.FromSeconds(FullLength);
        }

        public override void OffsetIdealTimeDiff(TimeSpan offset)
        {
            IdealTimeDiff = IdealTimeDiff.Add(offset);
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

        public List<WorkingSimpleStep> Flatten()
        {
            var ls = new List<WorkingSimpleStep>();
            foreach(var x in Children)
            {
                if (x is WorkingSimpleStep s)
                    ls.Add(s);
                else if (x is WorkingMultiStep m)
                    ls.AddRange(m.Flatten());
            }
            return ls;
        }

        public WorkingMultiStep(SavedRecipe recipe, string title, bool inOrder) : base(recipe)
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
        public override TimeSpan SetIdealTimeDiff(TimeSpan startDiff, TimeSpan? targetEnd)
        {
            Sort();
            int length = 0;
            if (InOrder)
            {
                foreach (var step in Children)
                {
                    step.SetIdealTimeDiff(startDiff + TimeSpan.FromSeconds(length), targetEnd);
                    length += step.FullLength;
                }
                return startDiff + TimeSpan.FromSeconds(length);
            }
            else
            {
                targetEnd ??= TimeSpan.FromSeconds(Children.First().FullLength);
                foreach (var step in Children)
                {
                    if(targetEnd.HasValue)
                    {
                        var diff = targetEnd.Value - startDiff;
                        length = (int)Math.Max(0, diff.TotalSeconds - step.FullLength);
                    }
                    step.SetIdealTimeDiff(startDiff + TimeSpan.FromSeconds(length), targetEnd);
                }
                return startDiff;//+ TimeSpan.FromSeconds(maxLength);
            }
        }

        public override void OffsetIdealTimeDiff(TimeSpan offset)
        {
            foreach (var x in Children)
                x.OffsetIdealTimeDiff(offset);
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

    #endregion
}
