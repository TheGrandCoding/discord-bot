using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Services;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    public class Food : AuthedAPIBase
    {
        public Food(APIContext c) : base(c, "food")
        {
            Service = Program.Services.GetRequiredService<FoodService>();
        }

        public FoodService Service { get; set; }

        string formatProductId(string id)
        {
            if(id.Length == 13)
            {
                return id[0]
                    + " " + id.Substring(1, 6)
                    + " " + id.Substring(7, 6);
            } else if(id.Length == 8)
            {
                return id.Substring(0, 4) + " " + id.Substring(4);
            }
            return id;
        }

        Classes.HTMLHelpers.DOMBase getProductInfo(Product product, Classes.HTMLHelpers.DOMBase container = null)
        {
            container ??= new TableData(null);

            string retailerName = Service.GetManufacturor(product.Id);

            if(retailerName != null)
            {
                container.Children.Add(new Span(cls: "badge") { RawText = retailerName });
            }
            container.Children.Add(new Span(cls: "product-name") { RawText = product.Name });

            return container;
        }

        TableData getExpirationInfo(InventoryItem item, bool full)
        {
            string text;
            var diff = item.ExpiresAt - DateTime.UtcNow;
            if (diff.TotalDays < -1)
            {
                text = $"expired {Math.Abs((int)diff.TotalDays)} days ago";
            }
            else if (item.ExpiresAt.Date.Equals(DateTime.UtcNow.Date))
            {
                text = "today";
            }
            else if (item.ExpiresAt.Date.Equals(DateTime.UtcNow.Date.AddDays(1)))
            {
                text = "tomorrow";
            }
            else if (diff.TotalDays < 7)
            {
                var nextSunday = DateTime.UtcNow.NextDay(DayOfWeek.Sunday);
                if(item.ExpiresAt< nextSunday)
                    text = $"this {item.ExpiresAt.DayOfWeek}";
                else
                    text = $"next {item.ExpiresAt.DayOfWeek}";
                text += $" {item.ExpiresAt:dd}{Program.GetDaySuffix(item.ExpiresAt.Day)}";

            }
            else if (diff.TotalDays < 60)
            {
                text = $"{item.ExpiresAt:dd}{Program.GetDaySuffix(item.ExpiresAt.Day)} {item.ExpiresAt:MMMM}";
            }
            else if (item.ExpiresAt.Year == 2050)
            {
                text = "n/a";
            }
            else
            {
                text = $"{item.ExpiresAt:MMMM yyyy}";
            }

            if(full && item.Frozen && (item.Product?.FreezingExtends ?? 0) > 0)
            {
                text += $"; original: {item.InitialExpiresAt:yyyy-MM-dd}";
            }

            if (item.Frozen)
            {
                return new TableData(null)
                {
                    Children =
                    {
                        new Img("/_/img/snowflake.png") {Style = "width: 32px;"},
                        new Span() { RawText = text }
                    }
                };
            }
            else
            {
                return new TableData(text);
            }
        }

        TableRow getRow(InventoryItem item, bool full)
        {
            var row = new TableRow();
            if (full)
            {
                row.WithCell(item.Id.ToString());
                row.WithCell(formatProductId(item.ProductId));
            }
            if (item.Product == null)
                row.WithCell("n/a");
            else
                row.Children.Add(getProductInfo(item.Product));
            row.WithCell($"{item.AddedAt:yyyy-MM-dd}");
            row.Children.Add(getExpirationInfo(item, full));
            var diff = item.ExpiresAt - DateTime.UtcNow;
            if (diff.TotalDays < 7)
                row.Class = "expires-soon";
            if (diff.TotalHours < 48)
                row.Class = "expires-imminently";

            if (Context.User != null)
            {
                row.Children.Add(new TableData(null)
                {
                    Children = {
                            new Input("button", "Delete", cls: "danger")
                            {
                                OnClick = $"removeInvItem({item.Id});"
                            },
                            new Input("button", item.Frozen ? "Unfreeze" : "Freeze", cls: "freeze")
                            {
                                OnClick = $"toggleFrozen({item.Id}, {item.Frozen.ToString().ToLower()});"
                            }
                        }
                });
            }
            return row;
        }

        string getNonGrouped(bool full)
        {
            var table = new Table();
            var hr = new TableRow();
            if (full)
            {
                hr.WithHeader("Inv. Id");
                hr.WithHeader("Product ID");
            }
            hr.WithHeader("Item");
            hr.WithHeader("Added");
            hr.WithHeader("Expires");
            if (Context.User != null)
                hr.WithHeader("");
            table.Children.Add(hr);
            var inv = Service.GetInventoryItems("default");
            foreach (var item in inv.OrderBy(x => x.ExpiresAt))
            {
                table.Children.Add(getRow(item, full));
            }
            return table.ToString(true);
        }

        string getGroupedInfo(bool full)
        {
            var inv = Service.GetInventoryItems("default");
            var grouped = new Dictionary<string, List<InventoryItem>>();

            foreach(var item in inv)
            {
                if (string.IsNullOrWhiteSpace(item.Product?.Tags ?? ""))
                    continue;
                foreach(var tag in item.Product.Tags.Split(';'))
                {
                    grouped.AddInner(tag, item);
                }
            }

            var table = new Table();
            var hr = new TableRow();
            if (full)
            {
                hr.WithHeader("Inv. Id");
                hr.WithHeader("Product ID");
            }
            hr.WithHeader("Item");
            hr.WithHeader("Added");
            hr.WithHeader("Expires");
            if (Context.User != null)
                hr.WithHeader("");
            table.Children.Add(hr);

            foreach((var groupName, var ls) in grouped)
            {
                table.Children.Add(new TableRow()
                {
                    Children =
                    {
                        new TableHeader(groupName) {ColSpan = hr.Children.Count.ToString()}
                    }
                });
                foreach(var item in ls.OrderBy(x => x.ExpiresAt))
                {
                    var row = getRow(item, full);
                    if(!string.IsNullOrWhiteSpace(item.Product.Tags))
                    {
                        var c = item.Product.Tags.Split(';')
                            .Where(cn => cn != groupName)
                            .ToList();
                        if(c.Count > 0)
                        {
                            var nameCell = row.Children[full ? 2 : 0] as TableData;
                            var warnSpan = new Span(cls: "product-warn");
                            warnSpan.Children.Add(new Break());
                            warnSpan.Children.Add(new RawObject($"(!) This item is also listed in other categories: {string.Join(", ", c)}"));
                            nameCell.Children.Add(warnSpan);
                        }
                    }
                    table.Children.Add(row);
                }
            }
            return table;
        }

        [Method("GET"), Path("/food")]
        [RequireApproval(false)]
        [RequireAuthentication(false, false)]
        public void Base(bool grouped = false, bool full = false)
        {
            var s = grouped
                ? getGroupedInfo(full)
                : getNonGrouped(full);

            string links = "";
            links += new Anchor($"/food?full={!full}&grouped={grouped}", full ? "View less information" : "View full information");
            links += " -- ";
            links += new Anchor($"/food?full={full}&grouped={!grouped}", grouped ? "View non-grouped" : "View grouped information");
            links += "<br/>";
            links += new Anchor($"/food/calendar", "View as calandar");

            if (Context.User != null)
            {
                links += " -- ";
                links += new Anchor("/food/scan", "Scan new item");
            }

            ReplyFile("base.html", 200, new Replacements()
                .Add("inventoryid", "default")
                .Add("table", s)
                .Add("links", links));
        }

        [Method("GET"), Path("/food/scan")]
        public void Scan()
        {
            ReplyFile("scan.html", 200);
        }

        [Method("GET"), Path("/food/enter")]
        public void Enter()
        {
            ReplyFile("enter.html", 200);
        }

        [Method("GET"), Path("/food/new")]
        public void NewFood(string code = null, string redirect = null)
        {
            if(code == null)
            {
                ReplyFile("new_product.html", 200, new Replacements()
                    .Add("code", "")
                    .Add("redirect", redirect));
                return;
            }
            code = code.Replace(" ", "");
            if (code.Length > 13)
                return;
            var item = Service.GetProduct(code);
            if(item == null)
            {
                var manufs = Service.GetManufacturor(code);
                if(!string.IsNullOrWhiteSpace(manufs))
                {
                    manufs = new Span() { Class = "badge", RawText = manufs };
                    manufs += "<br/>";
                } else
                {
                    manufs = "";
                }
                ReplyFile("new_product.html", 200, new Replacements()
                    .Add("code", formatProductId(code))
                    .Add("redirect", redirect)
                    .Add("manu", manufs));
            } else
            {
                var existing = Service.GetInventoryItems("default")
                    .Where(x => x.ProductId == item.Id)
                    .ToList();

                string tableStr = "";
                if(existing.Count > 0)
                {
                    var table = new Table();
                    table.Children.Add(new TableRow()
                        .WithHeader("Inventory Id")
                        .WithHeader("Added")
                        .WithHeader("Expires")
                        .WithHeader($"Freeze")
                        .WithHeader("Remove"));
                    foreach(var thing in existing)
                    {
                        var row = new TableRow();

                        row.WithCell($"{thing.Id}");
                        row.WithCell($"{thing.AddedAt:yyyy-MM-dd}");
                        var d = getExpirationInfo(thing, true);
                        d.Children.Add(new Span() { RawText = $" ({thing.ExpiresAt:yyyy-MM-dd})" });
                        row.Children.Add(d);

                        if(!thing.Frozen)
                        {
                            row.Children.Add(new TableData(null)
                            {
                                Children = {
                                    new Input("button", "Freeze", cls: "danger")
                                    {
                                        OnClick = $"markFrozen({thing.Id});"
                                    }
                                }
                            });
                        } else
                        {
                            row.Children.Add(new TableData(""));
                        }
                        row.Children.Add(new TableData(null)
                        {
                            Children = {
                                new Input("button", "Delete", cls: "danger")
                                {
                                    OnClick = $"removeInvItem({thing.Id});"
                                }
                            }
                        });

                        table.Children.Add(row);
                    }
                    tableStr = table.ToString(true);
                }

                ReplyFile("new_inventory.html", 200, new Replacements()
                    .Add("prodId", formatProductId(code))
                    .Add("prodName", getProductInfo(item))
                    .Add("redirect", redirect)
                    .Add("existing", tableStr));
            }
        }


        [Method("GET"), Path("/food/calendar")]
        [RequireApproval(false)]
        [RequireAuthentication(false, false)]
        public void Calendar()
        {
            InjectObjects = new List<Classes.HTMLHelpers.HTMLBase>();
            ReplyFile("calendar.html", 200);
        }
        
        [Method("GET"), Path("/food/recipes")]
        public void ViewRecipes()
        {
            var table = new Table()
            {
                Children =
                {
                    new TableRow()
                        .WithHeader("Ingredients")
                        .WithHeader("Steps")
                        .WithHeader("Actions")
                }
            };
            foreach(var x in Service.Recipes)
            {
                var r = new TableRow();

                var ing = new UnorderedList();
                foreach(var i in x.Ingredients)
                {
                    var prod = Service.GetProduct(i.Key);
                    if (prod == null)
                        continue;
                    ing.AddItem(new ListItem($"{i.Value}x ")
                    {
                        Children =
                        {
                            getProductInfo(prod, new Classes.HTMLHelpers.Objects.Span())
                        }
                    });
                }
                r.Children.Add(new TableData(null) { Children = { ing } });

                var steps = x.InOrder ? (Classes.HTMLHelpers.DOMBase)new OrderedList() : (Classes.HTMLHelpers.DOMBase)new UnorderedList();
                foreach(var s in x.Steps)
                {
                    if (steps is OrderedList ol)
                        ol.AddItem(s.GetListItem());
                    else if (steps is UnorderedList ul)
                        ul.AddItem(s.GetListItem());
                }
                r.Children.Add(new TableData(null) { Children = { steps } });

                r.Children.Add(new TableData(null)
                {
                    Children =
                    {
                        new Input("checkbox", id: x.Id.ToString(), cls: "recipe-selects") {
                            OnClick = "cbToggled(event)"
                        },
                        new Input("number", id: $"delay-{x.Id}") {
                            Style = "display: none"
                        }.WithTag("placeholder", "Offset (s)"),
                        new Input("button", "Delete") {OnClick = $"deleteRecipe({x.Id});"}
                    }
                });
                table.Children.Add(r);
            }

            var ongoing = new UnorderedList();
            foreach(var keypair in Service.OngoingRecipes)
            {
                ongoing.AddItem(new ListItem()
                {
                    Children =
                    {
                        new Anchor($"/food/ongoing-recipe?id={keypair.Key}", $"Ends at {keypair.Value.EstimatedEndAt}")
                    }
                });
            }
            var str = Service.OngoingRecipes.Count > 0 ? ongoing.ToString() : "";

            ReplyFile("recipe.html", 200, new Replacements().Add("table", table).Add("ongoing", str));
        }
        
        [Method("GET"), Path("/food/add-recipe")]
        public void NewRecipe()
        {
            ReplyFile("new_recipe.html", 200);
        }
        
        [Method("GET"), Path("/food/ongoing-recipe")]
        public void ViewOngoingRecipe(int id)
        {
            if(!Service.OngoingRecipes.TryGetValue(id, out var _))
            { // no recipe
                RespondRaw(LoadRedirectFile("/food/recipes"), System.Net.HttpStatusCode.Found);
            } else
            {
                ReplyFile("view_ongoing.html", 200, new Replacements().Add("id", id));
            }
        }

        [Method("GET"), Path("/food/ongoing-any")]
        public void ViewFirstOngoing()
        {
            if(Service.OngoingRecipes.Count > 0)
            {
                RespondRaw(LoadRedirectFile($"/food/ongoing-recipe?id={Service.OngoingRecipes.First().Key}"), System.Net.HttpStatusCode.Found);
                return;
            } else
            {
                ReplyFile("pending.html", 200);
            }
        }

        [Method("GET"), Path("/food/next")]
        public void ViewFutures()
        {
            ReplyFile("next.html", 200);
        }

        [Method("POST"), Path("/api/food/scanned")]
        [RequireApproval(false)]
        [RequireAuthentication(false)]
        public void APIScannedCode(string code, string token)
        {
            if(token != Program.Configuration["tokens:foodnotify"])
            {
                RespondRaw("", System.Net.HttpStatusCode.Unauthorized);
                return;
            }
            bool any = false;
            if(WSService.Server.WebSocketServices.TryGetServiceHost("/food-scan", out var host))
            {
                foreach (var session in host.Sessions.Sessions)
                {
                    if (session is Websockets.FoodScanWS ws)
                    {
                        try
                        {
                            ws.SendCode(code);
                            any = true;
                        }
                        catch (Exception ex)
                        {
                            Program.LogError(ex, "APIScannedCode");
                        }
                    }
                }
            }
            if(any)
            {
                RespondRaw("Redirected user.", 200);
            } else
            {
                RespondRaw("Notified user", 200);
                Service.NotifyScannedProduct(code).Wait();
            }
        }


        struct FutureData
        {
            public int Count { get; set; }
            public DateTime OldestAdded { get; set; }
            public DateTime OldestRemoved { get; set; }

            public double AddedPerWeek { get
                {
                    var diff = DateTime.Now - OldestAdded;
                    var weeks = diff.TotalDays / 7;
                    return Count / weeks;
                } }
            public double RemovedPerWeek
            {
                get
                {
                    var diff = DateTime.Now - OldestRemoved;
                    var weeks = diff.TotalDays / 7;
                    return Count / weeks;
                }
            }

            public double AverageTimeBetweenAdd { get; set; }

            public double AverageTimeBetweenRemove { get; set; }

            public double AverageLifetime { get; set; }

            public int EstimatedNeeded { get; set; }

            public JObject ToJson()
            {
                var jobj = new JObject();
                jobj["count"] = Count;
                jobj["add"] = AddedPerWeek;
                jobj["rem"] = RemovedPerWeek;
                jobj["avgAdd"] = AverageTimeBetweenAdd;
                jobj["avgRem"] = AverageTimeBetweenRemove;
                jobj["avgLifetime"] = AverageLifetime;
                jobj["estimation"] = EstimatedNeeded;
                return jobj;
            }

            public FutureData(List<HistoricItem> items)
            {
                OldestAdded = DateTime.MaxValue;
                OldestRemoved = DateTime.MaxValue;
                Count = items.Count;
                EstimatedNeeded = -1;

                var diffAdd = new List<TimeSpan>();
                var diffRem = new List<TimeSpan>();
                var diffLive = new List<TimeSpan>();

                DateTime? lastAdd = null;
                foreach(var x in items.OrderBy(x => x.AddedAt))
                {
                    if (x.AddedAt < OldestAdded)
                        OldestAdded = x.AddedAt;
                    if (x.RemovedAt < OldestRemoved)
                        OldestRemoved = x.RemovedAt;
                    diffLive.Add(x.RemovedAt - x.AddedAt);
                    if(lastAdd.HasValue)
                        diffAdd.Add(x.AddedAt - lastAdd.Value);
                    else
                        lastAdd = x.AddedAt;
                }

                AverageTimeBetweenAdd = diffAdd.Count == 0 ? 0 : diffAdd.Select(x => x.TotalMilliseconds).Average();

                DateTime? lastRem = null;
                foreach(var x in items.OrderBy(x => x.RemovedAt))
                {
                    if (lastRem.HasValue)
                        diffRem.Add(x.RemovedAt - lastRem.Value);
                    else lastRem = x.RemovedAt;
                }
                AverageTimeBetweenRemove = diffRem.Count == 0 ? 0 : diffRem.Select(x => x.TotalMilliseconds).Average();
                AverageLifetime = diffLive.Select(x => x.TotalMilliseconds).Average();

            }

        }

        [Method("GET"), Path("/api/food/future")]
        public void APIFutures(DateTime date)
        {
            var inventory = Service.GetInventoryItems("default");

            var productCache = new Dictionary<string, Product>();
            Func<string, Product> getProduct = (x) =>
            {
                if (productCache.TryGetValue(x, out var product))
                    return product;
                product = Service.GetProduct(x);
                productCache[x] = product;
                return product;
            };

            var missingItems = new JArray();

            foreach(var item in inventory)
            {
                if(item.ExpiresAt < date)
                {
                    var jb = new JObject();
                    var prod = getProduct(item.ProductId);
                    var tags = Service.GetManufacturor(item.ProductId);
                    string name = "";
                    if (tags != null)
                        name = $"({tags}) ";
                    name += prod?.Name ?? "n/a";
                    jb["name"] = name;
                    jb["expires"] = new DateTimeOffset(item.ExpiresAt).ToUnixTimeMilliseconds();
                    missingItems.Add(jb);
                }
            }

            var historic = Service.GetHistoricItems();

            var productDict = new Dictionary<string, List<HistoricItem>>();
            var tagDict = new Dictionary<string, List<HistoricItem>>();

            foreach(var item in historic)
            {
                productDict.AddInner(item.ProductId, item);
                var prod = getProduct(item.ProductId);
                if(prod != null)
                {
                    foreach (var tag in (prod.Tags ?? "").Split(';'))
                        if(!string.IsNullOrWhiteSpace(tag))
                            tagDict.AddInner(tag, item);
                }
            }

            var predicted = new JObject();
            var productData = new Dictionary<string, FutureData>();

            var differenceWeeks = (date - DateTime.Now).TotalDays / 7;

            foreach(var keypair in productDict)
            {
#if !DEBUG
                if (keypair.Value.Count <= 3) continue;
#endif

                var data = new FutureData(keypair.Value);

                var estimatedNumber = data.AddedPerWeek * differenceWeeks;
                var existing = inventory.Where(x => x.ProductId == keypair.Key).ToList();
                int numExisting = 0;
                foreach(var exist in existing)
                {
                    var estimatedRemoval = exist.AddedAt.AddMilliseconds(data.AverageLifetime);
                    if (estimatedRemoval < date)
                        numExisting++;
                }

                data.EstimatedNeeded = (int)Math.Round(estimatedNumber - numExisting);


                productData[keypair.Key] = data;
            }



            var tagData = new Dictionary<string, FutureData>();
            foreach(var keypair in tagDict)
            {
#if !DEBUG
                if (keypair.Value.Count <= 3) continue;
#endif
                var data = new FutureData(keypair.Value);

                var estimatedNumber = data.AddedPerWeek * differenceWeeks;
                var existingWithTag = inventory.Where(x => (getProduct(x.ProductId)?.Tags ?? "").Contains(keypair.Key)).ToList();
                
                int numExisting = 0;
                foreach (var exist in existingWithTag)
                {
                    if(productData.TryGetValue(exist.ProductId, out var itemData))
                    {
                        var estimatedRemoval = exist.AddedAt.AddMilliseconds(itemData.AverageLifetime);
                        if (estimatedRemoval < date)
                            numExisting++;
                    }
                }

                data.EstimatedNeeded = (int)Math.Round(estimatedNumber - numExisting);

                tagData[keypair.Key] = data;
            }

            predicted["products"] = productData.ToJson(x => x.ToJson());
            predicted["tags"] = tagData.ToJson(x => x.ToJson());

            var body = new JObject();
            body["expiring"] = missingItems;
            body["predicted"] = predicted;

            var thing = new JObject();
            foreach(var keypair in productCache)
            {
                thing[keypair.Key] = keypair.Value.ToJson();
            }
            body["products"] = thing;

            RespondJson(body);
        }


        [Method("GET"), Path("/api/food/calendar")]
        [RequireApproval(false)]
        [RequireAuthentication(false, false)]
        public void APIGetWeek(DateTime start, DateTime end)
        {
            start = start.ToUniversalTime();
            end = end.ToUniversalTime();
            using var db = Service.DB();
            var items = db.GetExpiresBetween(start, end);
            var jarray = new JArray();
            foreach(var item in items)
            {
                var t = new DateTimeOffset(item.ExpiresAt.Ticks, TimeSpan.Zero);
                var jobj = new JObject();
                jobj["id"] = $"{item.Id}";
                jobj["allDay"] = true;
                jobj["start"] = t.ToUnixTimeMilliseconds();
                jobj["title"] = item.Product.Name;
                if(!string.IsNullOrWhiteSpace(item.Product.Tags))
                    jobj["tags"] = new JArray(item.Product.Tags.Split(';'));
                jobj["product"] = item.Product.Id;
                jobj["frozen"] = item.Frozen;
                var man = Service.GetManufacturor(item.Product.Id);
                if(!string.IsNullOrWhiteSpace(man))
                    jobj["manu"] = man;
                jarray.Add(jobj);
            }
            RespondJson(jarray);
        }


        [Method("POST"), Path("/api/food/products")]
        public void NewProduct(string redirect, string productId, string productName, int extends)
        {
            if(redirect != "enter" && redirect != "scan")
            {
                RespondRaw("", 400);
                return;
            }
            productId = productId.Replace(" ", "");
            if(string.IsNullOrWhiteSpace(productId) || productId.Length > 13)
            {
                RespondRaw("Product ID or length invalid", 400);
                return;
            }
            if(string.IsNullOrWhiteSpace(productName) || productName.Length > 1024)
            {
                RespondRaw("Product name or length invalid", 400);
                return;
            }
            if(extends < 0 || extends > 180)
            {
                RespondRaw("Product extend expiration invalid", 400);
                return;
            }
            int? e = null;
            if (extends > 0)
                e = extends;
            var p = Service.AddProduct(productId, productName, "", e, "");
            RespondRaw(LoadRedirectFile($"/food/new?code={productId}&redirect={redirect}"), System.Net.HttpStatusCode.Found);
        }
        [Method("POST"),   Path("/api/food/inventory")]
        public void NewInventory(string redirect, string productId, string expires, string frozen = "off")
        {
            if (redirect != "enter" && redirect != "scan")
            {
                RespondRaw("", 400);
                return;
            }
            productId = productId.Replace(" ", "");
            var split = expires.Split('-');
            var date = new DateTime(int.Parse(split[0]),
                                    int.Parse(split[1]),
                                    int.Parse(split[2]), 0, 0, 0, DateTimeKind.Utc);

            Service.AddInventoryItem(productId, "default", date, frozen == "on");
            RespondRaw(LoadRedirectFile($"/food/{redirect}"), System.Net.HttpStatusCode.Found);
        }
        [Method("DELETE"), Path("/api/food/inventory")]
        public void DeleteInventory(int invId)
        {
            if(Service.DeleteInventoryItem(invId))
            {
                RespondRaw("", 200);
            } else
            {
                RespondRaw("", 404);
            }
        }

        [Method("PATCH"), Path("/api/food/inventory")]
        public void EditInventory(int invId, bool frozen)
        {
            using var db = Service.DB();
            var entity = db.Inventory.Find(invId);
            if (entity == null)
            {
                RespondRaw("", 404);
                return;
            }
            entity.Frozen = frozen;
            db.SaveChanges();
            RespondRaw("", 200);
        }

        SavedStep parseStep(JObject obj)
        {
            var saved = new SavedStep();
            saved.Description = obj.GetValue("description")?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(saved.Description))
                throw new ArgumentException($"Description cannot be null");
            if (obj.TryGetValue("children", out var children))
            {
                var arr = children as JArray;
                saved.Children = arr.Select(x => parseStep(x as JObject)).ToList();
                saved.InOrder = obj["order"]?.ToObject<bool?>() ?? false;
            } else
            {
                saved.Duration = obj.GetValue("duration")?.ToObject<int?>() ?? 0;
                saved.Delay = obj.GetValue("delay")?.ToObject<int?>() ?? 0;
            }
            return saved;
        }

        [Method("POST"), Path("/api/food/recipes")]
        public void AddRecipe()
        {
            var jobj = JObject.Parse(Context.Body);
            var recipe = new SavedRecipe();
            recipe.InOrder = jobj["order"].ToObject<bool>();

            if(!jobj.TryGetValue("ingredients", out var ingToken))
            {
                RespondRaw("Ingredients absent", 400);
                return;
            }
            if(!jobj.TryGetValue("steps", out var stepsA))
            {
                RespondRaw("Steps absent", 400);
                return;
            }
            var ingredients = ingToken as JArray;
            foreach(var x in ingredients)
            {
                var ing = x as JObject;
                var id = ing.GetValue("id")?.ToObject<string>();
                id = (id ?? "").Replace(" ", "");
                if(string.IsNullOrWhiteSpace(id))
                {
                    RespondRaw("Ingredient ID is null", 400);
                    return;
                }
                var units = ing.GetValue("unitsUsed")?.ToObject<int?>();
                if(!units.HasValue)
                {
                    RespondRaw("Units used is null", 400);
                    return;
                }

                var prod = Service.GetProduct(id);
                if(prod == null)
                {
                    RespondRaw($"No product exists with ID '{id}'", 400);
                    return;
                }

                recipe.Ingredients.Add(id, units.Value);
            }

            var steps = stepsA as JArray;
            foreach(var x in steps)
            {
                var step = parseStep(x as JObject);
                recipe.Steps.Add(step);
            }

            Service.Recipes.Add(recipe);
            Service.OnSave();

            RespondRaw("OK");
        }

        [Method("DELETE"), Path("/api/food/recipe")]
        public void DeleteRecipe(int id)
        {
            Service.DeleteRecipe(id);
            RespondRaw("OK", 200);
        }

        [Method("PUT"), Path("/api/food/recipes")]
        public void StartRecipe()
        {
            var dict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<int, int>>(Context.Body);
            var recipes = dict.Keys.Select(x => Service.Recipes.FirstOrDefault(r => r.Id == x)).ToList();
            if(recipes.Any(x => x == null))
            {
                RespondRaw("A recipe is null", 400);
                return;
            }
            if(recipes.Count == 0)
            {
                RespondRaw("No recipes selected", 400);
                return;
            }
            WorkingRecipe working;
            if(recipes.Count == 1)
            {
                working = recipes[0].ToWorking();
            } else
            {
                working = new WorkingRecipe(recipes.ToArray());
                var combined = new WorkingMultiStep(null, "Combined Root", false);
                foreach (var x in recipes)
                    combined.WithChild(x.ToChild());
                combined.SetIdealTimeDiff(TimeSpan.Zero, null);

                foreach(var step in combined.Children)
                {
                    var offset = dict.GetValueOrDefault(step.Recipe.Id, 0);
                    if(offset != 0)
                        step.OffsetIdealTimeDiff(TimeSpan.FromSeconds(offset));
                }

                var flattened = combined.Flatten();
                working.WithSteps(flattened);
            }
            Service.OngoingRecipes.TryAdd(working.Id, working);
            RespondRaw($"{working.Id}", 203);
        }


    }
}
