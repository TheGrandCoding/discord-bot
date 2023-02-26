using DiscordBot.Classes;
using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Services;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
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
            InjectObjects.Add(new Classes.HTMLHelpers.Objects.PageLink("stylesheet", "text/css", "/_/css/food.css"));
        }

        public FoodService Service { get; set; }

        Dictionary<string, Product> prodCache = new Dictionary<string, Product>();
        Product getProductCached(string id)
        {
            if (prodCache.TryGetValue(id, out var p)) return p;
            p = Service.GetProduct(id);
            prodCache[id] = p;
            return p;
        }

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

        Classes.HTMLHelpers.DOMBase getProductInfo(Product product, int usedUp, Classes.HTMLHelpers.DOMBase container = null)
        {
            container ??= new TableData(null);

            string retailerName = Service.GetManufacturor(product.Id);

            if(retailerName != null)
            {
                container.Children.Add(new Span(cls: "badge") { RawText = retailerName });
            }
            int amount = product.Uses - usedUp;
            container.Children.Add(new Span(cls: "product-name") { RawText = (amount > 1 ? $"{amount}x " : "") + product.Name });
            

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
                        new Img("/_/img/snowflake.png") {Style = "width: 32px; vertical-align: middle"},
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
                row.Children.Add(getProductInfo(item.Product, item.TimesUsed));
            row.WithCell($"{item.AddedAt:yyyy-MM-dd}");
            row.Children.Add(getExpirationInfo(item, full));
            var diff = item.ExpiresAt - DateTime.UtcNow;
            if (diff.TotalDays < 7)
                row.Class = "expires-soon";
            if (diff.TotalHours < 48)
                row.Class = "expires-imminently";

            if (Context.User != null)
            {
                bool shouldUse = (item.Product?.Uses ?? 0) - item.TimesUsed > 1;
                row.Children.Add(new TableData(null)
                {
                    Children = {
                            new Input("button", shouldUse ? "Use" : "Delete", cls: "danger")
                            {
                                OnClick = $"removeInvItem({item.Id}, {(shouldUse ? "true" : "false")});"
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


        class ItemGroups
        {
            public ItemGroups(IEnumerable<string> path)
            {
                OwnPath = path.ToArray();
            }
            public string[] OwnPath { get; set; }
            public Dictionary<string, ItemGroups> Groups = new();
            public List<InventoryItem> Items = new();

            public void Add(InventoryItem item, List<string> tags)
            {
                if (tags.Count == 0)
                {
                    Items.Add(item);
                }
                else
                {
                    var next = tags.First();
                    tags.RemoveAt(0);
                    ItemGroups child;
                    if (!Groups.TryGetValue(next, out child))
                    {
                        child = new ItemGroups(OwnPath.Append(next));
                        Groups[next] = child;
                    }
                    child.Add(item, tags);
                }
            }

            public HTMLBase ToHTML(bool full, bool actionColumn, Func<InventoryItem, bool, TableRow> getRow)
            {
                string groupName = string.Join('/', OwnPath);
                var div = new Div();
                if(OwnPath.Length > 0)
                {
                    div.Children.Add(new Header(OwnPath.Length).WithRawText(String.Join('/', OwnPath)));
                }
                if(Items.Count > 0)
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
                    if (actionColumn)
                        hr.WithHeader("");
                    table.Children.Add(hr);

                    foreach (var item in Items.OrderBy(x => x.ExpiresAt))
                    {
                        var row = getRow(item, full);
                        if (!string.IsNullOrWhiteSpace(item.Product.Tags))
                        {
                            var c = item.Product.Tags.Split(';')
                                .Where(cn => cn != groupName)
                                .ToList();
                            if (c.Count > 0)
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
                    div.Children.Add(table);
                }
                if(Groups.Count > 0)
                {
                    foreach((var key, var value) in Groups)
                    {
                        div.Children.Add(value.ToHTML(full, actionColumn, getRow));
                    }
                }
                return div;
            }
        }

        string getGroupedInfo(bool full)
        {
            var inv = Service.GetInventoryItems("default");
            var grouped = new ItemGroups(new string[] {});

            foreach(var item in inv)
            {
                if (string.IsNullOrWhiteSpace(item.Product?.Tags ?? ""))
                    continue;
                foreach(var tag in item.Product.Tags.Split(';'))
                {
                    grouped.Add(item, tag.Split(' ', '/').ToList());
                }
            }




            return grouped.ToHTML(full, Context.User != null, getRow);
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
            links += " -- ";
            links += new Anchor("/food/menu", "View current menu");

            links += " -- ";
            if (Context.User == null)
            {
                links += new StrongText() { Children = { new Anchor("/login", "Login") } };
            } else 
            {
                links += new Anchor("/food/scan", "Scan new item");
                links += " -- ";
                links += new Anchor("/food/recipes", "View recipes");
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
                    .Add("prodName", getProductInfo(item, 0))
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

        static CacheDictionary<string, Product> productCache = new();
        HTMLBase getShortDesc(SavedRecipe recipe)
        {
            if (!string.IsNullOrWhiteSpace(recipe.Title)) return new StrongText(recipe.Title);
            Product product = null;
            if(recipe.Ingredients.Count == 1)
            {
                var prodKey = recipe.Ingredients.Keys.First();
                if(!productCache.TryGetValue(prodKey, out product))
                {
                    product = Service.GetProduct(prodKey);
                    productCache[prodKey] = product;
                }
            }
            return new StrongText(product?.Name ?? recipe.Id.ToString());
        }
        HTMLBase getShortDesc(int recipeId)
        {
            var recipe = Service.Recipes.FirstOrDefault(x => x.Id == recipeId);
            if (recipe == null) return new StrongText($"<{recipeId}>");
            return getShortDesc(recipe);
        }

        Img getCatalystImg(string catalyst)
        {
            var fName = Program.GetSafePath(catalyst.Replace(' ', '_'));
            return new Img($"/_/img/cat_{fName}.png", cls: "catalyst")
                .WithTag("title", catalyst)
                .WithTag("alt", catalyst) as Img;
        }

        List<string> getCatalysts(int recipeId)
        {
            var recipe = Service.Recipes.FirstOrDefault(x => x.Id == recipeId);
            var ls = new List<string>();
            if (recipe == null) return ls;
            if (recipe.Catalyst != null) ls.Add(recipe.Catalyst);
            if(recipe.Children != null)
            {
                foreach(var id in recipe.Children.Keys)
                {
                    ls.AddRange(getCatalysts(id));
                }
            }
            return ls;
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
            foreach(var x in Service.Recipes.OrderBy(x => x.Kind).ThenBy(x => x.Id))
            {
                var r = new TableRow();

                var ing = new UnorderedList();
                foreach(var i in x.Ingredients)
                {
                    var prod = Service.GetProduct(i.Key);
                    if (prod == null)
                        continue;

                    var listItem = new ListItem();
                    var c = new Classes.HTMLHelpers.Objects.Span();
                    if (i.Value.Amount > 1)
                        c.Children.Add(new RawObject($"{i.Value.Amount}x "));
                    var info = getProductInfo(prod, 0, c);
                    if(i.Value.Frozen)
                        info.Children.Add(new Span(cls: "frozen").WithRawText(" (from Frozen)"));
                    listItem.Children.Add(info);
                    listItem.WithTag("data-id", prod.Id);
                    ing.AddItem(listItem);
                }
                var firstTd = new TableData(null);
                firstTd.Children.Add(new StrongText($"{x.Id}: "));

                if (!string.IsNullOrWhiteSpace(x.Title))
                    firstTd.Children.Add(new StrongText(x.Title));

                if(x.Children.Count > 0)
                {
                    firstTd.Children.Add(new Span().WithRawText(", uses: "));
                    foreach((var id, _) in x.Children)
                    {
                        firstTd.Children.Add(getShortDesc(id));
                        firstTd.Children.Add(new Span().WithRawText(", "));
                    }
                }

                firstTd.Children.Add(ing);
                r.Children.Add(firstTd);

                var steps = x.InOrder ? (Classes.HTMLHelpers.DOMBase)new OrderedList() : (Classes.HTMLHelpers.DOMBase)new UnorderedList();
                foreach(var s in x.Steps)
                {
                    if (steps is OrderedList ol)
                        ol.AddItem(s.GetListItem());
                    else if (steps is UnorderedList ul)
                        ul.AddItem(s.GetListItem());
                }
                var stepData = new TableData(null);
                var catImgs = new List<Img>();
                var catalysts = getCatalysts(x.Id);
                foreach(var cat in catalysts.Distinct())
                {
                    var img = getCatalystImg(cat);
                    stepData.Children.Add(img);
                }


                stepData.Children.Add(steps);
                r.Children.Add(stepData);

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
                        new Input("button", "Edit") {OnClick = $"editRecipe({x.Id});"},
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
        public void NewRecipe(int? modifyId = null)
        {
            ReplyFile("new_recipe.html", 200, new Replacements().Add("modifying", modifyId));
        }
        
        [Method("GET"), Path("/food/ongoing-recipe")]
        public void ViewOngoingRecipe(int id)
        {
            if(!Service.OngoingRecipes.TryGetValue(id, out var _))
            { // no recipe
                RespondRedirect("/food/recipes");
            } else
            {
                ReplyFile("new_ongoing.html", 200, new Replacements().Add("id", id));
            }
        }

        [Method("GET"), Path("test-recipe")]
        public void ViewTestRecipe()
        {
            ReplyFile("new_ongoing.html", 200);
        }

        [Method("GET"), Path("/food/ongoing-any")]
        public void ViewFirstOngoing()
        {
            if(Service.OngoingRecipes.Count > 0)
            {
                RespondRedirect($"/food/ongoing-recipe?id={Service.OngoingRecipes.First().Key}");
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

        [Method("GET"), Path("/food/products")]
        public void ViewProducts()
        {
            var table = new Table()
                .WithHeaderColumn("Id")
                .WithHeaderColumn("Name")
                .WithHeaderColumn("Manufactoror");
            foreach(var prod in Service.GetAllProducts())
            {
                table.WithRow(new Anchor($"/food/new?code={prod.Id}&redirect=enter", prod.Id), prod.Name, Service.GetManufacturor(prod.Id) ?? "");
            }
            ReplyFile("products.html", 200, new Replacements().Add("table", table));
        }

        int itemN = 0;
        Classes.HTMLHelpers.HTMLBase getItemInfo(WorkingMenuItem menuitem, bool edit)
        {
            var item = menuitem.Item;
            var div = new Div(id: $"{item.Id}-{itemN++}", "placed item draggable");
            div.WithTag("title", $"Id: {item.Id}; Product: {item.ProductId}");
            div.WithTag("data-uses", menuitem.Uses.ToString());
            div.WithTag("data-id", item.Id.ToString());
            var manu = Service.GetManufacturor(item.ProductId);
            if (menuitem.Uses > 1)
                div.Children.Add(new Span(cls: "uses").WithRawText($"{menuitem.Uses}x "));
            if(manu != null)
                div.Children.Add(new Span(cls: "manu") { RawText = manu });
            if(item.Frozen)
            {
                div.Children.Add(new Img("/_/img/snowflake.png")
                {
                    Style = "width: 32px; vertical-align: middle"
                });
            }
            div.Children.Add(new Span() { RawText = item.Product.Name });
            if(edit)
            {
                div.WithTag("draggable", "true");
                div.WithTag("ondragstart", "onDragStart(event);");
                div.WithTag("onclick", "onItemClick(event);");
            }
            return div;
        }

        void viewMenuActual(bool edit)
        {
            var table = new Table();
            var replacements = new Replacements();

            if (Service.WorkingMenu == null)
            {
                replacements.Add("title", "Selector");

                table.WithHeaderColumn("Title");

                if(edit)
                {
                    foreach ((var key, var value) in Service.Menus)
                    {
                        var div = new Div();
                        div.Children.Add(new Anchor("#", value.Title ?? $"{key}") { OnClick = $"selectMenu({key});" });
                        div.Children.Add(new Anchor($"/food/menu/new?overwrite={key}", " (Edit)"));
                        table.WithRow(div);
                    }
                } else
                {
                    table.WithRow("No menu selected.");
                }
            }
            else
            {
                table.WithHeaderColumn("Day");
                var _groups = Service.WorkingMenu.GetGroups();
                foreach (var group in _groups)
                    table.WithHeaderColumn(group);

                int dayIndex = 0;
                foreach (var day in Service.WorkingMenu.Days)
                {
                    var date = day.Date;
                    var row = new TableRow(id: $"day-{dayIndex}");
                    if(date.Year == DateTime.UtcNow.Year && date.DayOfYear == DateTime.UtcNow.DayOfYear)
                        row.ClassList.Add("today-row");

                    var dayTd = new TableData($"{date.DayOfWeek} {date.Day}{Program.GetDaySuffix(date.Day)}");
                    var maninp = new Input("checkbox") { OnClick = $"togglemanual(event);" };
                    if (day.ManualOverride) maninp.WithTag("checked", null);
                    dayTd.Children.Add(maninp);

                    row.Children.Add(dayTd);

                    if (day.Items.TryGetValue("*", out var ls))
                    {
                        TableData data = new TableData(null, cls: "shared") { ColSpan = $"{_groups.Length}" };
                        if (day.Text.TryGetValue("*", out var text))
                            data.Children.Add(new StrongText(text));
                        data.WithTag("data-group", "*");
                        data.WithTag("data-date", dayIndex.ToString());
                        if(edit)
                        {
                            data.WithTag("ondragover", "onDragOver(event)");
                            data.WithTag("ondrop", "onDrop(event)");
                        }
                        foreach (var item in ls)
                        {
                            if (item == null || item.Item == null || item.Item?.InventoryId == null) continue;
                            data.Children.Add(getItemInfo(item, edit));
                        }
                        row.Children.Add(data);
                    }
                    else
                    {
                        foreach (var group in _groups)
                        {
                            TableData data = new TableData(null);
                            data.WithTag("data-group", group);
                            data.WithTag("data-date", dayIndex.ToString());
                            if(edit)
                            {
                                data.WithTag("ondragover", "onDragOver(event)");
                                data.WithTag("ondrop", "onDrop(event)");
                            }
                            if (day.Text.TryGetValue(group, out var x))
                                data.Children.Add(new StrongText(x));
                            if (day.Items.TryGetValue(group, out ls))
                            {
                                foreach (var item in ls)
                                {
                                    if (item == null || item.Item == null || item.Item?.InventoryId == null) continue;
                                    data.Children.Add(getItemInfo(item, edit));
                                }
                            }
                            row.Children.Add(data);
                        }
                    }


                    var inp = new Input("checkbox")
                    {
                        Checked = day.Items.ContainsKey("*"),
                        OnClick = edit ? "toggleShare(event);" : "return false",
                        Style = "float: right"
                    };
                    row.Children.Last().Children.Add(inp);
                    table.Children.Add(row);
                    dayIndex++;
                }

                if(edit)
                {
                    replacements.Add("title", new Anchor($"/food/menu/new?overwrite={Service.WorkingMenu.FromMenu}", Service.WorkingMenu.Title));
                } else
                {
                    replacements.Add("title", Service.WorkingMenu.Title);
                }
            }
            replacements.Add("edit", edit ? "true" : "");

            ReplyFile("menu.html", 200, replacements.Add("table", table.ToString(true)));
        }

        [Method("GET"), Path("/food/menu")]
        public void ViewCurrentMenu()
        {
            viewMenuActual(true);
        }

        [Method("GET"), Path("/food/menu/readonly")]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        public void ViewCurrentMenuReadonly()
        {
            viewMenuActual(false);
        }

        [Method("GET"), Path("/food/menu/new")]
        public void ViewNewMenu(int? overwrite = null)
        {
            ReplyFile("new_menu.html", 200, 
                new Replacements()
                .Add("modify", overwrite.HasValue ? overwrite.Value.ToString() : ""));
        }





        [Method("POST"), Path("/api/food/scanned")]
        [RequireApproval(false)]
        [RequireAuthentication(false)]
        public void APIScannedCode(string code, string token)
        {
            if(token != Program.Configuration["tokens:foodnotify"])
            {
                RespondRaw($"Token '{token}' is not authorised", System.Net.HttpStatusCode.Unauthorized);
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
        public void APIFutures(DateTime date, DateTime prev)
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

            var recent = new JObject();

            var productDict = new Dictionary<string, List<HistoricItem>>();
            var tagDict = new Dictionary<string, List<HistoricItem>>();

            foreach(var item in historic)
            {
                productDict.AddInner(item.ProductId, item);
                var prod = getProduct(item.ProductId);
                if (prod != null)
                {
                    foreach (var tag in (prod.Tags ?? "").Split(';'))
                        if(!string.IsNullOrWhiteSpace(tag))
                            tagDict.AddInner(tag, item);
                    if (item.RemovedAt > prev)
                    {
                        var _j = new JObject();
                        var manu = Service.GetManufacturor(prod.Id);
                        string s = manu == null ? "" : $"({manu}) ";
                        _j["name"] = s + prod.Name;
                        _j["removed"] = new DateTimeOffset(item.RemovedAt).ToUnixTimeMilliseconds();
                        recent[item.Id.ToString()] = _j;
                    }
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
            body["recent"] = recent;

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
        public void NewProduct(string redirect, string productId, string productName, int extends, int uses)
        {
            var _error = APIErrorResponse.InvalidFormBody();
            if(redirect != "enter" && redirect != "scan")
            {
                RespondError(_error.Child("redirect").EndChoices("enter", "scan").Build(), 400);
                return;
            }
            productId = productId.Replace(" ", "");
            if(string.IsNullOrWhiteSpace(productId) || productId.Length > 13)
            {
                RespondError(_error.Child("productId").EndError("INVALID", "Null, empty, whitespace or longer than 13 characters").Build(), 400);
                return;
            }
            if(string.IsNullOrWhiteSpace(productName) || productName.Length > 1024)
            {
                RespondError(_error.Child("productName").EndError("INVALID", "Null, empty, whitespace or longer than 1024 characters").Build(), 400);
                return;
            }
            if(extends < 0 || extends > 180)
            {
                RespondError(_error.Child("extends").EndRange(0, 180), 400);
                return;
            }
            if(uses < 1)
            {
                RespondError(_error.Child("uses").EndRange(1, int.MaxValue), 400);
                return;
            }
            int? e = null;
            if (extends > 0)
                e = extends;
            var p = Service.AddProduct(productId, productName, "", e, uses, "");
            RespondRedirect($"/food/new?code={productId}&redirect={redirect}");
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

            Service.AddInventoryItem(productId, FoodService.DefaultInventoryId, date, frozen == "on");
            RespondRedirect($"/food/{redirect}");
        }

        [Method("DELETE"), Path("/api/food/inventory")]
        public void DeleteInventoryUses(int invId, int? uses = null)
        {
            if(!uses.HasValue)
            {
                RespondError(APIErrorResponse.InvalidQueryParams().Child("uses").EndRequired());
                return;
            }
            if(Service.AddUsesInventoryItem(invId, uses.Value, DateTime.UtcNow))
            {
                RespondRaw("", 200);
            } else
            {
                RespondRaw("", 404);
            }
        }

        [Method("POST"), Path("/api/food/query")]
        public void SearchInventory(string query, string sort = null)
        {
            var items = new List<InventoryItem>();
            using (var db = Service.DB())
                items = db.Inventory.Where(x => x.InventoryId == FoodService.DefaultInventoryId).ToList();

            var selected = new List<InventoryItem>();
            var jarr = new JArray();
            foreach (var inv in items)
            {
                if(inv.Id.ToString() == query || (inv.Product?.Name ?? "").Contains(query, StringComparison.InvariantCultureIgnoreCase) || (inv.Product?.Tags ?? "").Contains(query, StringComparison.InvariantCultureIgnoreCase))
                {
                    selected.Add(inv);
                }
            }
            if(sort == "expires")
            {
                selected = selected.OrderBy(x => x.ExpiresAt).ToList();
            }
            foreach(var inv in selected)
                jarr.Add(inv.ToJson(true, Service));
            RespondJson(jarr);
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

        [Method("GET"), Path("/api/food/product")]
        public void SearchProducts(string query)
        {
            List<Product> products;
            using (var db = Service.DB())
            {
                products = db.Products.AsAsyncEnumerable().Where(x => x.Id == query || x.Name.Contains(query, StringComparison.InvariantCultureIgnoreCase)).ToListAsync().Result;
            }
            var jarray = new JArray();

            if(products.Count == 0)
            {
                RespondRaw("[]", 400);
                return;
            }

            foreach(var prod in products)
            {
                jarray.Add(prod.ToJson());
            }
            RespondJson(jarray);
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
        public void AddRecipe(int? overwrite = null)
        {
            var jobj = JObject.Parse(Context.Body);
            var recipe = new SavedRecipe(overwrite.GetValueOrDefault(-1));
            recipe.InOrder = jobj["order"].ToObject<bool>();
            var _err = APIErrorResponse.InvalidFormBody();

            if(!jobj.TryGetValue("ingredients", out var ingToken))
            {
                RespondError(_err.Child("ingredients").EndRequired());
                return;
            }
            if(!jobj.TryGetValue("steps", out var stepsA))
            {
                RespondError(_err.Child("steps").EndRequired());
                return;
            }
            if(jobj.TryGetValue("children", out var child))
            {
                recipe.Children = new Dictionary<int, int>();
                int _ind = 0;
                foreach (var c in (child as JArray))
                {
                    var o = c as JObject;
                    recipe.Children[o["id"].ToObject<int>()] = o["offset"].ToObject<int>();
                }
            } else
            {
                recipe.Children = null;
            }
            var catalyst = jobj.GetValue("catalyst")?.ToObject<string>();
            recipe.Catalyst = string.IsNullOrWhiteSpace(catalyst) ? null : catalyst;
            var title = jobj.GetValue("title")?.ToObject<string>();
            recipe.Title = string.IsNullOrWhiteSpace(title) ? null : title;
            var ingredients = ingToken as JArray;
            var _ing = _err.Child("ingredients");
            int _index = 0;
            foreach (var x in ingredients)
            {
                var err = _ing.Child(_index++);

                var ing = x as JObject;
                var id = ing.GetValue("id")?.ToObject<string>();
                id = (id ?? "").Replace(" ", "");
                if(string.IsNullOrWhiteSpace(id))
                {
                    RespondError(err.Child("id").EndRequired());
                    return;
                }
                var units = ing.GetValue("unitsUsed")?.ToObject<int?>();
                if(!units.HasValue)
                {
                    RespondError(err.Child("unitsUsed").EndRequired());
                    return;
                }
                var frozen = ing.GetValue("frozen")?.ToObject<bool?>();
                if(!frozen.HasValue)
                {
                    RespondError(err.Child("frozen").EndRequired());
                    return;
                }
                    
                var prod = Service.GetProduct(id);
                if(prod == null)
                {
                    RespondError(err.Child("id").EndError("MISSING", "No product by that ID exists"));
                    return;
                }

                var si = new SavedIngredient(units.Value, frozen.Value);
                recipe.Ingredients.Add(id, si);
            }


            var steps = stepsA as JArray;
            foreach(var x in steps)
            {
                var step = parseStep(x as JObject);
                recipe.Steps.Add(step);
            }

            if (recipe.Children.Count > 0)
            {
                if (recipe.Steps.Count > 0)
                {
                    RespondError(_err.EndError("CONFLICT", "Cannot have children and steps specified"));
                    return;
                }
                if(recipe.Ingredients.Count > 0)
                {
                    RespondError(_err.EndError("CONFLICT", "Cannot have children and ingredients specified"));
                    return;
                }
            }
            if(string.IsNullOrWhiteSpace(recipe.Title))
            {
                if(recipe.Ingredients.Count == 0)
                {
                    RespondError(_err["title"].EndRequired("Title or ingredients must be given"));
                    return;
                }
            }
            if (recipe.Catalyst == null && recipe.Children.Count == 0)
            {
                RespondError(_err.Child("catalyst").EndRequired("Catalyst or child recipes is required"));
                return;
            }


            if (overwrite.HasValue)
            {
                Service.DeleteRecipe(recipe.Id);
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

        [Method("GET"), Path("/api/food/recipe")]
        public void GetRecipe(int id)
        {
            var recipe = Service.Recipes.FirstOrDefault(x => x.Id == id);
            if(recipe == null)
            {
                RespondRaw("", 404);
                return;
            }
            var jobj = new JObject();
            jobj["title"] = recipe.Title;
            jobj["catalyst"] = recipe.Catalyst;
            jobj["order"] = recipe.InOrder;

            if(recipe.Children != null)
            {
                var ch = new JArray();
                foreach (var child in recipe.Children)
                {
                    var jo = new JObject();
                    jo["id"] = child.Key;
                    jo["offset"] = child.Value;
                    ch.Add(jo);
                }
                jobj["children"] = ch;
            }

            var steps = new JArray();
            foreach(var step in recipe.Steps)
            {
                var stepJ = new JObject();
                stepJ["description"] = step.Description;
                stepJ["duration"] = step.Duration;
                stepJ["delay"] = step.Delay;
                stepJ["order"] = step.InOrder;
                steps.Add(stepJ);
            }
            jobj["steps"] = steps;


            var ingredients = new JArray();
            foreach(var ingPair in recipe.Ingredients)
            {
                var ingJ = new JObject();
                ingJ["id"] = ingPair.Key;
                ingJ["unitsUsed"] = ingPair.Value.Amount;
                ingJ["frozen"] = ingPair.Value.Frozen;
                ingredients.Add(ingJ);
            }
            jobj["ingredients"] = ingredients;

            RespondJson(jobj);
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

            var confirmedRecipes = new List<SavedRecipe>();
            while(recipes.Count > 0)
            {
                for(int i = 0; i < recipes.Count; i++)
                {
                    var recipe = recipes[i];
                    recipes.RemoveAt(i);
                    i--;
                    if(recipe.Children != null && recipe.Children.Count > 0)
                    {
                        foreach(var keypair in recipe.Children)
                        {
                            if (dict.ContainsKey(keypair.Key)) continue;
                            var r = Service.Recipes.FirstOrDefault(x => x.Id == keypair.Key);
                            if(r != null)
                            {
                                dict[keypair.Key] = keypair.Value;
                                recipes.Add(r);
                            }
                        }
                    } else
                    {
                        confirmedRecipes.Add(recipe);
                    }
                }
            }

            WorkingRecipeCollection working;
            if(confirmedRecipes.Count == 1)
            {
                working = Service.ToWorkingRecipe(confirmedRecipes.First(), dict);
            } else
            {
                working = Service.ToWorkingRecipe(confirmedRecipes, dict);
            }
            RespondRaw($"{working.Id}", 203);
        }


        [Method("POST"), Path("/api/food/menus")]
        public void AddMenu(int? modify = null)
        {
            var body = JObject.Parse(Context.Body);
            var errorMaker = APIErrorResponse.InvalidFormBody();
            var menu = new SavedMenu();
            if (body.TryGetValue("title", out var title))
                menu.Title = title.ToObject<string>();


            var daysArray = body["days"] as JArray;
            var dayError = errorMaker.Child("days");
            if(daysArray == null)
            {
                RespondError(dayError.EndRequired(), 400);
                return;
            }

            for(int dayI = 0; dayI < daysArray.Count; dayI++)
            {
                var _error = dayError.Child(dayI);
                var day = daysArray[dayI] as JObject;
                if(day == null)
                {
                    RespondError(_error.EndError("NOT_NULL", "This field must be an object"), 400);
                    return;
                }
                var menuDay = new SavedMenuDay();
                if (day.TryGetValue("text", out var text))
                    menuDay.Text = text.ToObject<Dictionary<string, string>>();

                var dayItems = day["items"] as JObject;
                var itemsError = _error.Child("items");
                if(dayItems == null)
                {
                    RespondError(itemsError.EndError("NOT_NULL", "This field must be an object"), 400);
                    return;
                }

                foreach(var keypair in dayItems)
                {
                    var kpError = itemsError.Child(keypair.Key);
                    var value = keypair.Value as JArray;

                    for(int itemI = 0; itemI < value.Count; itemI++)
                    {
                        var iError = kpError.Child(itemI);
                        var item = value[itemI] as JObject;
                        if(!item.TryGetValue("type", out var type))
                        {
                            RespondError(iError.Child("type").EndRequired());
                            return;
                        }
                        if(!item.TryGetValue("value", out var itemValue))
                        {
                            RespondError(iError.Child("type").EndRequired());
                            return;
                        }
                        if(!item.TryGetValue("uses", out var itemUses))
                        {
                            RespondError(iError.Child("uses").EndRequired());
                            return;
                        }

                        SavedMenuItem menuItem = null;

                        string typeValue = type.ToObject<string>();
                        if(typeValue == "id")
                        {
                            var idmenuItem = new SavedMenuIdItem();
                            idmenuItem.Ids = itemValue.ToObject<List<string>>();
                            foreach(var id in idmenuItem.Ids)
                            {
                                if(getProductCached(id) == null)
                                {
                                    RespondError(iError.EndError("MISSING", $"No product by '{id}' exists"), 400);
                                    return;
                                }
                            }
                            menuItem = idmenuItem;
                        } else if(typeValue == "tag")
                        {
                            menuItem = new SavedMenuTagItem()
                            {
                                Tags = itemValue.ToObject<List<string>>()
                            };
                        } else
                        {
                            RespondError(iError.Child("type").EndChoices("id", "tag"), 400);
                            return;
                        }
                        menuItem.AmountUsed = itemUses.ToObject<int>();

                        menuDay.Items.AddInner(keypair.Key, menuItem);

                    }
                }

                menu.Days.Add(menuDay);
            }
            if (modify.HasValue)
                menu.Id = modify.Value;
            Service.Menus[menu.Id] = menu;
            Service.OnSave();
            RespondRaw("{}", 200);
        }

        [Method("POST"), Path("/api/food/menu")]
        public void SelectMenu(int id)
        {
            if(Service.WorkingMenu != null)
            {
                RespondRaw("Cannot overwrite current menu", 400);
                return;
            }
            if(!Service.Menus.TryGetValue(id, out var menu))
            {
                RespondRaw("Menu does not exist", 400);
                return;
            }
            menu.Fulfill(Service, FoodService.DefaultInventoryId, DateTime.UtcNow.NextDay(DayOfWeek.Monday).Date, true);
            Service.OnSave();
            RespondRaw("Ok", 200);
        }

        [Method("GET"), Path("/api/food/menu")]
        public void ApiGetMenu(int id)
        {
            if(!Service.Menus.TryGetValue(id, out var menu))
            {
                RespondRaw("null", 404);
                return;
            }
            RespondJson(menu.ToJson());
        }

        struct moveData
        {
            public int ToDay;
            public int FromDay;
            public string ToGroup;
            public string FromGroup;
            public int Id;
            public int? Uses;
        }

        [Method("POST"), Path("/api/food/menu/shared")]
        public void MenuToggleShare(int day)
        {
            if (Service.WorkingMenu == null)
            {
                RespondRaw("No current menu", 400);
                return;
            }
            var d = Service.WorkingMenu.Days.ElementAtOrDefault(day);
            if(d.Items.TryGetValue("*", out var ls))
            {
                var groups = Service.WorkingMenu.GetGroups();
                d.Items = new Dictionary<string, List<WorkingMenuItem>>();
                foreach (var g in groups)
                    d.Items.Add(g, new List<WorkingMenuItem>());
            }
            else
            {
                d.Items = new Dictionary<string, List<WorkingMenuItem>>()
                {
                    {"*", new List<WorkingMenuItem>()}
                };
            }
            d.ManualOverride = true;
            Service.OnSave();
            RespondRaw("", 200);
        }

        [Method("POST"), Path("/api/food/menu/move")]
        public void MoveMenuItem()
        {
            if(Service.WorkingMenu == null)
            {
                RespondRaw("No current menu", 400);
                return;
            }
            var data = JsonConvert.DeserializeObject<moveData>(Context.Body);
            WorkingMenuItem item;
            if (data.FromGroup != null)
            {

                var fromD = Service.WorkingMenu.Days[data.FromDay];
                var fromG = fromD.Items[data.FromGroup];

                item = fromG.FirstOrDefault(x => x?.Item?.Id == data.Id);
                fromG.Remove(item);
            } else
            {
                var invItem = Service.GetInventoryItem(data.Id);
                item = new WorkingMenuItem()
                {
                    Item = invItem,
                    Uses = data.Uses.GetValueOrDefault(1)
                };
            }

            var toD = Service.WorkingMenu.Days[data.ToDay];
            toD.ManualOverride = true;

            toD.Items.AddInner(data.ToGroup, item);
            Service.OnSave();
            RespondRaw("OK");
        }

        [Method("PATCH"), Path("/api/food/menu/manual")]
        public void SetMenuDayManual(int day, bool manual)
        {
            var d = Service.WorkingMenu.Days[day];
            d.ManualOverride = manual;
            Service.OnSave();
            RespondRaw("OK");
        }
    
        [Method("DELETE"), Path("/api/food/menu/item")]
        public void DeleteMenuItem(string group, int day, int id)
        {
            if (Service.WorkingMenu == null)
            {
                RespondRaw("No current menu", 400);
                return;
            }
            var mDay = Service.WorkingMenu.Days[day];
            var mGroup = mDay.Items[group];
            mGroup.RemoveAll(x => x?.Item?.Id == id);
            Service.OnSave();
            RespondRaw("OK");
        }

        [Method("PATCH"), Path("/api/food/menu/item")]
        public void EditMenuItem(string group, int day, int id, int uses)
        {
            if (Service.WorkingMenu == null)
            {
                RespondRaw("No current menu", 400);
                return;
            }
            if(uses < 1)
            {
                RespondError(APIErrorResponse.InvalidQueryParams().Child("uses").EndError("INVALID", "Cannot be below 1"));
                return;
            }
            var mDay = Service.WorkingMenu.Days[day];
            var mGroup = mDay.Items[group];
            var item = mGroup.FirstOrDefault(x => x?.Item?.Id == id);
            if(item == null) 
            {
                RespondError(APIErrorResponse.InvalidQueryParams().Child("item").EndError("NOT FOUND", "No item found at the day, group and ID specified."));
                return;
            }
            item.Uses = uses;
            Service.OnSave();
            RespondRaw("OK");
        }

    }
}
