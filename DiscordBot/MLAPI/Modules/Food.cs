using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Services;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
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

        [Method("GET"), Path("/food")]
        [RequireApproval(false)]
        [RequireAuthentication(false, false)]
        public void Base(bool full = false)
        {
            var table = new Table();
            var hr = new TableRow();
            if(full)
            {
                hr.WithHeader("Inv. Id");
                hr.WithHeader("Product ID");
            }
            hr.WithHeader("Item");
            hr.WithHeader("Added");
            hr.WithHeader("Expires");
            if(Context.User != null)
                hr.WithHeader("");
            table.Children.Add(hr);
            var inv = Service.GetInventory("default");
            foreach(var item in inv.OrderBy(x => x.ExpiresAt))
            {
                var row = new TableRow();
                if(full)
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

                if(Context.User != null)
                {
                    row.Children.Add(new TableData(null)
                    {
                        Children = {
                            new Input("button", "Delete", cls: "danger")
                            {
                                OnClick = $"removeInvItem({item.Id});"
                            }
                        }
                    });
                }

                table.Children.Add(row);
            }
            var s = table.ToString(true);
            ReplyFile("base.html", 200, new Replacements()
                .Add("inventoryid", "default")
                .Add("table", s));
        }

        [Method("GET"), Path("/food/scan")]
        public void Scan()
        {
            ReplyFile("scan.html", 200);
        }

        [Method("GET"), Path("/food/new")]
        public void NewFood(string code)
        {
            code = code.Replace(" ", "");
            if (code.Length > 13)
                return;
            var item = Service.GetProduct(code);
            if(item == null)
            {
                ReplyFile("new_product.html", 200, new Replacements()
                    .Add("code", formatProductId(code)));
            } else
            {
                ReplyFile("new_inventory.html", 200, new Replacements()
                    .Add("prodId", formatProductId(code))
                    .Add("prodName", getProductInfo(item)));
            }
        }

        [Method("POST"), Path("/api/food/products")]
        public void NewProduct(string productId, string productName)
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
            var p = Service.AddProduct(productId, productName, "");
            RespondRaw(LoadRedirectFile($"/food/new?code={productId}"), System.Net.HttpStatusCode.Found);
        }
        [Method("POST"),   Path("/api/food/inventory")]
        public void NewInventory(string productId, string expires, string frozen)
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
            using var db = Service.DB();
            var entity = db.Inventory.Find(invId);
            if(entity == null)
            {
                RespondRaw("", 404);
                return;
            }
            db.Inventory.Remove(entity);
            db.SaveChanges();
            RespondRaw("", 200);
        }
    }
}
