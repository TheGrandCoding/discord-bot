using Discord;
using DiscordBot.Classes.Converters;
using DiscordBot.Utils;
using JsonSubTypes;
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
using DiscordBot.Classes;
using DiscordBot.Classes.DbContexts;

namespace DiscordBot.Services
{
    public class FoodService : SavedService
    {
        public ConcurrentDictionary<string, List<string>> Manufacturers { get; set; } = new();
        public ConcurrentBag<SavedRecipe> Recipes { get; set; } = new();


        public ConcurrentDictionary<int, SavedMenu> Menus { get; set; } = new();



        public ConcurrentDictionary<int, WorkingRecipeCollection> OngoingRecipes { get; set; } = new();

        public ConcurrentDictionary<ulong, CancellationTokenSource> NotifyCancels { get; set; } = new();


        public const string DefaultInventoryId = "default";


        private WorkingMenu _workingMenu;

        public WorkingMenu GetWorkingMenu(IServiceProvider services)
        {
            var db = services.GetFoodDb("GetWorkingMenu");
            _workingMenu.FlushCache(db);
            return _workingMenu;
        }
        public void SetWorkingMenu(WorkingMenu menu)
        {
            _workingMenu = menu;
        }

        public FoodDbContext DB()
        {
            return Program.GlobalServices.GetRequiredService<FoodDbContext>();
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

        public WorkingRecipeCollection ToWorkingRecipe(List<SavedRecipe> recipes, Dictionary<int, int> offsetDict, string title = null)
        {
            WorkingRecipeCollection working = WorkingRecipeCollection.FromSaved(offsetDict, recipes.ToArray());
            OngoingRecipes.TryAdd(working.Id, working);
            return working;
        }
        public WorkingRecipeCollection ToWorkingRecipe(SavedRecipe recipe, Dictionary<int, int> offsetDict)
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

        public async Task<IMessageChannel> getLogChannel()
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

        public EmbedBuilder getYesterdayUsedItems(WorkingMenu workingMenu)
        {
            var yes = DateTime.UtcNow.AddDays(-1);
            var yesterday = workingMenu.Days.FirstOrDefault(x => x.Date.IsSameDay(yes));
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

        [Cron("6,12", "0")]
        public void SendMenuNotifs(int hour)
        {
            using var scope = Program.GlobalServices.CreateScope();
            var workingMenu = GetWorkingMenu(scope.ServiceProvider);
            if (workingMenu == null) return;


            var embeds = new List<Embed>();
            MessageComponent components = null;
            var now = DateTime.UtcNow;
            var mention = false;
            if(hour < 12)
            {
                // morning reminder
                var today = workingMenu.Days.FirstOrDefault(x => x.Date.IsSameDay(now));
                embeds.Add(toEmbed(today, true, out mention).Build());
            } else
            {
                now = now.AddDays(1);
                WorkingMenuDay tomorrow;
                tomorrow = workingMenu.Days.FirstOrDefault(x => x.Date.IsSameDay(now));
                if (tomorrow == null)
                {
                    attemptFullfill(workingMenu, false);
                    tomorrow = workingMenu.Days.FirstOrDefault(x => x.Date.IsSameDay(now));
                    if(tomorrow == null)
                    {
                        Error("Tomorrow was still null even after adding new menu days?");
                        var sb = new StringBuilder();
                        sb.Append($"Days are:\n");
                        foreach(var x in workingMenu.Days)
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
                var src = CancellationTokenSource.CreateLinkedTokenSource(Program.GetToken());
                NotifyCancels[msg.Id] = src;
                Task.Run(async () =>
                {
                    try
                    {
                        while (!src.Token.IsCancellationRequested)
                        {
                            var now = DateTime.Now;
                            DateTime next;
                            if (now.Hour < 18)
                            {
                                next = new DateTime(now.Year, now.Month, now.Day, now.Hour + 1, 0, 0);
                            }
                            else
                            {
                                next = now.AddMinutes(5);
                            }
                            await Task.Delay(next - now, src.Token);
                            await lc.SendMessageAsync("Defrost. @everyone");
                        }
                    } finally
                    {
                        Info("Exiting Notify loop");
                    }
                });
            }
        }

        void attemptFullfill(WorkingMenu menu, bool forceLog)
        {
            if(Menus.TryGetValue(menu.NextComingUp, out var savedMenu))
                savedMenu.Fulfill(this, menu, DefaultInventoryId, DateTime.UtcNow.NextDay(DayOfWeek.Monday).Date, forceLog);
        }

        void doFreezerChecks(WorkingMenu menu, FoodDbContext db)
        {
            attemptFullfill(menu, DateTime.UtcNow.DayOfWeek == DayOfWeek.Sunday);
            var inventory = db.GetInventory(DefaultInventoryId);
            var usedOnDate = menu.GetItemsUsed();

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
            using var scope = Program.GlobalServices.CreateScope();
            var menu = GetWorkingMenu(scope.ServiceProvider);
            if (menu == null) return;
            // set uses on yesterday's stuff
            var yesE = getYesterdayUsedItems(menu);
            if (yesE != null)
            {
                var lc = await getLogChannel();
                var components = new ComponentBuilder()
                    .WithButton("Confirm", "food:uses:confirm", ButtonStyle.Success)
                    .WithButton("Refresh", "food:uses:refresh", ButtonStyle.Secondary)
                    .Build();
                await lc.SendMessageAsync(embed: yesE.Build(), components: components);
            }

            try
            {
                doFreezerChecks(menu, scope.ServiceProvider.GetFoodDb("freezer"));
            }
            finally
            {
                OnSave();
            }
        }



        public override string GenerateSave()
        {
            var dict = new Dictionary<string, List<string>>(Manufacturers);
            var recipList = new List<SavedRecipe>(Recipes);
            using var scope = Program.GlobalServices.CreateScope();
            var sv = new foodSave()
            {
                manufacturerPrefixes = dict,
                recipes = recipList,
                menus = Menus.Values.ToList(),
                curMenu = GetWorkingMenu(scope.ServiceProvider)
            };
            return Program.Serialise(sv, conv: new InventoryItemConverter());
        }
        
        
        
        public override void OnReady(IServiceProvider services)
        {
            var sv = Program.Deserialise<foodSave>(ReadSave(), new InventoryItemConverter());
            Manufacturers = new ConcurrentDictionary<string, List<string>>(sv.manufacturerPrefixes ?? new Dictionary<string, List<string>>());
            Recipes = new ConcurrentBag<SavedRecipe>(sv.recipes ?? new List<SavedRecipe>());
            Menus = new ConcurrentDictionary<int, SavedMenu>();
            foreach (var x in sv.menus)
                Menus[x.Id] = x;
            var cpy = sv.curMenu.Copy();
            using (var scope = Program.GlobalServices.CreateScope())
                cpy.FlushCache(scope.ServiceProvider.GetFoodDb("onReady"));
            SetWorkingMenu(cpy);
#if DEBUG
            //OnDailyTick();
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
            serv ??= Program.GlobalServices.GetRequiredService<FoodService>();
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
                srv ??= Program.GlobalServices.GetRequiredService<FoodService>();
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

        class orderData
        {
            public SavedMenuItem Item { get; set; }
            public string Group { get; set; }
            public WorkingMenuDay Day { get; set; }
            public int AmountFulfilled { get; set; }
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
                workingDay.Text = new (menuDay.Text);
                foreach (var keypair in menuDay.Items)
                {
                    log.AppendLine($"  {keypair.Key}: ");
                    if (!workingDay.Items.TryGetValue(keypair.Key, out var existing))
                        existing = new List<WorkingMenuItem>();
                    foreach (var item in keypair.Value)
                    {
                        log.Append($"   - {item.Type}");
                        var amended = item.AmendFulfilled(existing);
                        if(amended == null)
                        {
                            log.Append(" (already done)");
                            continue;
                        } else
                        {
                            log.Append($" (x{amended.AmountUsed})");
                        }
                        log.Append("\n");
                        var d = new orderData()
                        {
                            Item = amended,
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
                    data.AmountFulfilled += data.Item.AmountUsed;
                    added += 1;
                }
            }
            log.AppendLine("");
            log.AppendLine($"Checking done:");
            foreach(var grouping in items.GroupBy(x => x.Day))
            {
                var day = grouping.Key;
                var isFulfilled = grouping.All(x => x.AmountFulfilled >= x.Item.AmountUsed);
                log.AppendLine($"Day {day.Date}: {isFulfilled}");
                if(isFulfilled)
                {
                    day.ManualOverride = true;
                }
            }
        }

        public void FlushCache(FoodDbContext db)
        {
            foreach(var day in Days)
            {
                foreach(var ls in day.Items.Values)
                {
                    for(int i = 0; i < ls.Count; i++)
                    {
                        var item = ls[i];
                        if(item == null || item.Item == null)
                        {
                            ls.RemoveAt(i);
                            i--;
                            continue;
                        }
                        var updated = db.GetInventoryItem(item.Item.Id);
                        if(updated == null)
                        {
                            ls.RemoveAt(i);
                            i--;
                            continue;
                        }
                        ls[i].Item = updated;
                    }
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
                foreach (var key in x.Text.Keys)
                {
                    if (key != "*" && ls.Contains(key) == false) ls.Add(key);
                }
            }
            ls.Sort();
            return ls.ToArray();
        }

        internal WorkingMenu Copy()
        {
            return new WorkingMenu(Title)
            {
                Days = Days.Copy(x => x.Copy()),
                FromMenu = FromMenu,
                NextComingUp = NextComingUp
            };
        }
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
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

        internal WorkingMenuDay Copy()
        {
            return new WorkingMenuDay(Date, Text.Copy())
            {
                ManualOverride = ManualOverride,
                Items = Items.Copy(x => x.Copy(y => y.Copy()))
            };
        }

        internal string GetDebuggerDisplay()
        {
            var sb = new StringBuilder();
            sb.Append(Date.ToString("yyyy/MM/dd HH:mm:ss"));
            sb.Append($", {ManualOverride}");
            foreach ((var key, var txt) in Text)
                sb.Append($" {key}={txt}");
            return sb.ToString();
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

        internal WorkingMenuItem Copy()
        {
            return new WorkingMenuItem()
            {
                Item = Item,
                Uses = Uses
            };
        }
    }

    public class SavedMenu
    {
        public string Title { get; set; }
        public List<SavedMenuDay> Days { get; set; } = new List<SavedMenuDay>();

        public int Id { get; set; }
        private static int _id;

        Dictionary<string, TValue> correctDict<TValue>(Dictionary<string, TValue> dict)
        {
            if(dict.ContainsKey("*"))
            {
                var keys = dict.Keys.Where(x => x != "*").ToArray();
                foreach(var key in keys)
                {
                    dict.Remove(key);
                }
            }
            return dict;
        }

        public SavedMenu()
        {
            Id = System.Threading.Interlocked.Increment(ref _id);
        }
        [JsonConstructor]
        public SavedMenu(int id, List<SavedMenuDay> days)
        {
            Id = id;
            if (id > _id)
                _id = Id;
            Days = days;
            if(Days != null)
            {
                foreach(var d in Days)
                {
                    d.Text = correctDict(d.Text);
                    d.Items = correctDict(d.Items);
                }
            }
        }


        public void Fulfill(FoodService service, WorkingMenu menu, string inventoryId, DateTime? startdate, bool forceLog)
        {
            var log = new StringBuilder();

            log.AppendLine("== Starting menu conversion ==");
            if(menu == null)
            {
                menu = new WorkingMenu("");
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
                service.SetWorkingMenu(menu);
                var p = Program.GetTempPath("log.txt");
                System.IO.File.WriteAllText(p, log.ToString());
                Program.SendLogFileAsync(p, channel: service.getLogChannel().Result).Wait();
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

        public abstract SavedMenuItem AmendFulfilled(List<WorkingMenuItem> existing);
    }

    public class SavedMenuIdItem : SavedMenuItem
    {
        public SavedMenuIdItem()
        {
            Priority = 1;
        }
        public override string Type => "Id";

        public List<string> Ids { get; set; }

        public override SavedMenuItem AmendFulfilled(List<WorkingMenuItem> existing)
        {
            if (existing.Count == 0) return this;
            var needed = this.AmountUsed;
            foreach (var item in existing)
                if(Ids.Contains(item.Item?.Id.ToString() ?? ""))
                    needed -= item.Uses;
            if(needed == 0) return null; // already fulfilled everything
            return new SavedMenuIdItem()
            {
                AmountUsed = needed,
                Ids = Ids,
                Priority = Priority
            };
        }

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

        public override SavedMenuItem AmendFulfilled(List<WorkingMenuItem> existing)
        {
            if (existing.Count == 0) return this;
            var needed = this.AmountUsed;
            foreach (var item in existing)
            {
                var inv = item.Item;
                if (string.IsNullOrWhiteSpace(inv.Product?.Tags ?? ""))
                    continue;
                foreach (var t in inv.Product?.Tags.Split(';'))
                {
                    if (Tags.Contains(t))
                    {
                        needed -= item.Uses;
                        continue;
                    }
                }
            }
            if (needed == 0) return null; // already fulfilled everything
            return new SavedMenuTagItem()
            {
                AmountUsed = needed,
                Tags = Tags,
                Priority = Priority
            };
        }

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

        [JsonProperty("catalyst")]
        public string Catalyst { get; set; }

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

    public class WorkingRecipeCollection
    {
        private static int _id = 1;
        public int Id { get; set; }
        public List<WorkingRecipeGroup> RecipeGroups { get; set; } = new();

        public Dictionary<int, int> Offsets { get; set; }

        public DateTimeOffset EstimatedEndAt { get
            {
                var remain = RecipeGroups.Select(x => x.SumRemainLength()).Max();
                return DateTimeOffset.Now.AddSeconds(remain);
            } }

        public static WorkingRecipeCollection FromSaved(Dictionary<int, int> offsets, params SavedRecipe[] init)
        {
            var col = new WorkingRecipeCollection();
            col.Offsets = offsets;
            col.Id = System.Threading.Interlocked.Increment(ref _id);
            var grouped = init.GroupBy(x => x.Catalyst);
            foreach (var grouping in grouped)
            {
                var recipe = WorkingRecipeGroup.FromSaved(grouping.ToArray());
                col.RecipeGroups.Add(recipe);
            }
            return col;
        }

        public ulong? StartedAt { get; set; }

        private bool _init = false;
        public void Initialise()
        {
            if (_init) return;
            _init = true;

            var smallestOffset = 0;
            foreach((var key, var value) in Offsets)
            {
                if(value < smallestOffset)
                    smallestOffset = value;
            }
            var longest = 0;
            foreach (var group in RecipeGroups)
            {
                var length = group.SumRemainLength();
                if(length > longest) longest = length;
            }
            foreach(var group in RecipeGroups)
            {
                group.FlattenForEnd(longest + (smallestOffset * -1), Offsets);
            }
        }

        public JObject ToJson()
        {
            var jobj = new JObject();

            foreach(var group in RecipeGroups)
            {
                jobj[group.Catalyst] = group.ToJson();
            }

            return jobj;
        }
    }

    public class WorkingRecipeGroup
    {
        public WorkingRecipeGroup(string catalyst)
        {
            Catalyst = catalyst;
            Recipes = new();
        }
        public static WorkingRecipeGroup FromSaved(params SavedRecipe[] init)
        {
            var cat = init.Select(x => x.Catalyst).Distinct().ToArray();
            if (cat.Length != 1) throw new ArgumentException($"{nameof(init)} must be only one catalyst");
            var wg = new WorkingRecipeGroup(cat[0]);
            foreach(var recipe in init)
            {
                var working = WorkingRecipe.FromSaved(recipe);
                wg.Recipes.Add(working);
            }
            return wg;
        }
        public string Catalyst { get; set; }
        public List<WorkingRecipe> Recipes { get; private set; }
        public List<WorkingRecipeStep> SimpleSteps { get; private set; }
        public int DelayTime { get; private set; }

        public bool? Muted { get; set; }
        public bool Alarm { get; set; }

        public int? CompletedAt { get; set; }

        public ulong? StartedAt { get; set; }
        public ulong? AdvancedAt { get; set; }

        private int _sumLength(bool original)
        {
            var longest = 0;
            foreach (var recipe in Recipes)
            {
                int length = 0;
                if (original)
                {
                    length = recipe.SumOriginalLength();
                }
                else
                {
                    length = recipe.SumRemainLength();
                }
                if (length > longest)
                    longest = length;
            }
            return longest;
        }
        public int SumOriginalLength() => _sumLength(true);
        public int SumRemainLength() => _sumLength(false);
    
        public void FlattenForEnd(int targetEnd, Dictionary<int, int> offsets)
        { // e.g. targetEnd is 200 seconds
            var simpleSteps = new List<WorkingRecipeStep>();
            int? lastTime = null;
            foreach(var recipe in Recipes)
            {
                var delay = (targetEnd + offsets.GetValueOrDefault(recipe.RecipeId, 0)) - recipe.SumRemainLength();
                foreach(var step in recipe.Steps)
                {
                    step.TentativeStartTime = delay;
                    simpleSteps.OrderedInsert(step, (other, inserting) => other.TentativeStartTime < inserting.TentativeStartTime);
                
                    if(step.State != WorkingState.Complete)
                    {
                        delay += step.Remaining;
                    }

                    lastTime = step.TentativeStartTime + step.Remaining;
                }
            }

            if(simpleSteps.Last().Duration > 0)
            {
                var dishUp = new WorkingRecipeStep("Dish up", 0);
                dishUp.TentativeStartTime = lastTime ?? 0;
                simpleSteps.Insert(simpleSteps.Count, dishUp);
            }
            this.DelayTime = simpleSteps.First().TentativeStartTime ?? 0;
            this.SimpleSteps = simpleSteps.ToList();

            int? nextStart = null;
            for(int i = this.SimpleSteps.Count - 1; i >= 0; i--)
            {
                var step = this.SimpleSteps[i];
                if(nextStart.HasValue)
                {
                    step.Duration = nextStart.Value - step.TentativeStartTime.Value;
                    step.Remaining = step.Duration;
                }
                nextStart = step.TentativeStartTime.Value;
            }
            // go through and merge steps that start at the same time.
            int idx = 0;
            while(idx < (this.SimpleSteps.Count - 1))
            {
                var current = this.SimpleSteps[idx];
                var next = this.SimpleSteps[idx + 1];
                if(current.TentativeStartTime == next.TentativeStartTime)
                {
                    current.Text += " & " + next.Text;
                    current.Duration += next.Duration;
                    current.Remaining += next.Remaining;
                    this.SimpleSteps.RemoveAt(idx + 1);
                } else
                {
                    idx++;
                }

            }
        }
   
        
        public JObject ToJson()
        {
            var jobj = new JObject();
            int modify = 0;
            if(AdvancedAt.HasValue)
            {
                var now = (ulong)DateTimeOffset.Now.ToUnixTimeMilliseconds();
                var diff = now - AdvancedAt.Value; // how long the current step has been going on for.
                modify = (int)(diff / 1000) * -1;
            }


            jobj["catalyst"] = Catalyst;
            jobj["delayTime"] = DelayTime;
            if (Muted.HasValue) jobj["muted"] = Muted.Value;
            jobj["alarm"] = Alarm;
            //if(SimpleSteps == null)
            //    jobj["recipes"] = new JArray(Recipes.Select(x => x.ToJson(modify)).ToArray());
            //else

            var jarr = new JArray();
            if(this.SimpleSteps != null)
            {
                foreach(var step in this.SimpleSteps)
                {
                    if(step.State == WorkingState.Ongoing)
                        jarr.Add(step.ToJson(modify));
                    else
                        jarr.Add(step.ToJson(0));
                }
            }
            jobj["steps"] = jarr;
            if (CompletedAt.HasValue) jobj["completedAt"] = CompletedAt.Value;
            return jobj;
        }
    
    }

    public enum WorkingState
    {
        Pending,
        Ongoing,
        Complete
    }

    public class WorkingRecipe
    {
        public static WorkingRecipe FromSaved(SavedRecipe recipe)
        {
            var wr = new WorkingRecipe(recipe.Catalyst);
            wr.RecipeId = recipe.Id;
            foreach(var step in recipe.Steps)
            {
                wr.WithStep(step.Description, (step.Duration + step.Delay) ?? 0);
            }
            return wr;
        }
        public WorkingRecipe(string cat)
        {
            Catalyst = cat;
            Steps = new List<WorkingRecipeStep>();
            Current = 0;
        }
        public WorkingRecipe WithStep(string text, int duration = 0)
        {
            var s = new WorkingRecipeStep(text, duration);
            Steps.Add(s);
            return this;
        }

        public int RecipeId { get; set; } = 0;
        public List<WorkingRecipeStep> Steps { get; set; }
        public int Current { get; set; }
        public string Catalyst { get; set; }

        public WorkingRecipeStep WorkingOn { get
            {
                if (this.Current >= this.Steps.Count) return null;
                return this.Steps[this.Current];
            } }
        public WorkingRecipeStep NextUp { get
            {
                var n = Current + 1;
                if (n >= Steps.Count) return null;
                return Steps[n];
            } }

        private int _sumLength(bool original)
        {
            var s = 0;
            foreach(var step in Steps)
            {
                if(original)
                {
                    s += step.Duration;
                } else
                {
                    s += step.Remaining;
                }
            }
            return s;
        }
        public int SumOriginalLength() => _sumLength(true);
        public int SumRemainLength() => _sumLength(false);

        public JObject ToJson(int modifyRemainBy)
        {
            var jobj = new JObject();
            jobj["catalyst"] = Catalyst;
            jobj["current"] = Current;
            jobj["steps"] = new JArray(Steps.Select(x => x.ToJson(modifyRemainBy)).ToArray());
            return jobj;
        }
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class WorkingRecipeStep
    {
        public WorkingRecipeStep(string text, int duration)
        {
            Text = text;
            Duration = duration;
            Remaining = duration;
            State = WorkingState.Pending;
                
        }
        public string Text { get; set; }
        public int Duration { get; set; }
        public int Remaining { get; set; }
        public WorkingState State { get; set; }
        public ulong? StartedAt { get; set; }
        public int? TentativeStartTime { get; set; }

        public void Tick(int elapsed)
        {
            Remaining -= elapsed;
        }
    
        public JObject ToJson(int modifyRemaining)
        {
            var jobj = new JObject();

            jobj["text"] = Text;
            jobj["duration"] = Duration;
            jobj["remain"] = Remaining + modifyRemaining;
            jobj["state"] = State.ToString();
            if(StartedAt.HasValue)
                jobj["startedAt"] = StartedAt.Value.ToString(); // due to int limit
            if(TentativeStartTime.HasValue)
                jobj["tentativeStart"] = TentativeStartTime;

            return jobj;
        }

        private string GetDebuggerDisplay()
        {
            return $"({Remaining}/{Duration}) {TentativeStartTime} - {Text}";
        }
    }

    #endregion
}
