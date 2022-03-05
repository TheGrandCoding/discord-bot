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

        TableData getProductInfo(Product product)
        {
            var data = new TableData(null);

            string retailerName = Service.GetManufacturor(product.Id);

            if(retailerName != null)
            {
                data.Children.Add(new Span(cls: "badge") { RawText = retailerName });
            }
            data.Children.Add(new Span(cls: "product-name") { RawText = product.Name });

            return data;
        }

        TableData getExpirationInfo(InventoryItem item)
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
            row.Children.Add(getExpirationInfo(item));
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
            var inv = Service.GetInventory("default");
            foreach (var item in inv.OrderBy(x => x.ExpiresAt))
            {
                table.Children.Add(getRow(item, full));
            }
            return table.ToString(true);
        }

        string getGroupedInfo(bool full)
        {
            var inv = Service.GetInventory("default");
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
                        var c = item.Product.Tags.Split(';');
                        if(c.Length > 1)
                        {
                            var nameCell = row.Children[full ? 2 : 0] as TableData;
                            var warnSpan = new Span(cls: "product-warn")
                            {
                                RawText = $"<br/> (!) Duplicated in multiple categories: {string.Join(", ", c)}"
                            };
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

        [Method("GET"), Path("/food/new")]
        public void NewFood(string code = null)
        {
            if(code == null)
            {
                ReplyFile("new_product.html", 200, new Replacements()
                    .Add("code", ""));
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
                    .Add("manu", manufs));
            } else
            {
                var existing = Service.GetInventory("default")
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
                        var d = getExpirationInfo(thing);
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
        public void NewProduct(string productId, string productName, int extends)
        {
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
            RespondRaw(LoadRedirectFile($"/food/new?code={productId}"), System.Net.HttpStatusCode.Found);
        }
        [Method("POST"),   Path("/api/food/inventory")]
        public void NewInventory(string productId, string expires, string frozen = "off")
        {
            productId = productId.Replace(" ", "");
            var split = expires.Split('-');
            var date = new DateTime(int.Parse(split[0]),
                                    int.Parse(split[1]),
                                    int.Parse(split[2]), 0, 0, 0, DateTimeKind.Utc);

            Service.AddInventoryItem(productId, "default", date, frozen == "on");
            RespondRaw(LoadRedirectFile($"/food/scan"), System.Net.HttpStatusCode.Found);
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

    }
}
