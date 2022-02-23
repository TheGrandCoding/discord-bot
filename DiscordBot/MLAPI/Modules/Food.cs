﻿using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Services;
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

        [Method("GET"), Path("/food")]
        public void Base(bool full = false)
        {
            var table = new Table();
            var hr = new TableRow();
            if(full)
            {
                hr.WithHeader("Inv. Id");
                hr.WithHeader("Product ID");
            }
            hr.WithHeader("Added");
            hr.WithHeader("Expires");
            hr.WithHeader("");
            table.Children.Add(hr);
            var inv = Service.GetInventory("default");
            foreach(var item in inv.OrderBy(x => x.ExpiresAt))
            {
                var row = new TableRow();
                if(full)
                {
                    row.WithCell(item.Id.ToString());
                    row.WithCell(item.ProductId);
                }
                row.WithCell(item.Product?.Name ?? "unknown");
                row.WithCell($"{item.AddedAt:yyyy-MM-dd}");
                row.WithCell($"{item.ExpiresAt:yyyy-MM-dd}");
                var diff = item.ExpiresAt - DateTime.UtcNow;
                if (diff.TotalDays < 7)
                    row.Class = "expires-soon";
                if (diff.TotalHours < 48)
                    row.Class = "expires-imminently";

                row.Children.Add(new TableRow()
                {
                    Children = {
                        new Input("button", "Delete", cls: "danger")
                        {
                            OnClick = $"removeInvItem({item.Id});"
                        }
                    }
                });

                table.Children.Add(row);
            }
            ReplyFile("base.html", 200, new Replacements()
                .Add("inventoryid", "default")
                .Add("table", table.ToString()));
        }

        [Method("GET"), Path("/food/scan")]
        public void Scan()
        {
            ReplyFile("scan.html", 200);
        }

        [Method("GET"), Path("/food/new")]
        public void NewFood(string code)
        {
            if (code.Length > 13)
                return;
            var item = Service.GetProduct(code);
            if(item == null)
            {
                ReplyFile("new_product.html", 200, new Replacements()
                    .Add("code", code));
            } else
            {
                ReplyFile("new_inventory.html", 200, new Replacements()
                    .Add("product", item));
            }
        }

        [Method("POST"), Path("/api/food/products")]
        public void NewProduct(string productId, string productName)
        {
            var p = Service.AddProduct(productId, productName, "");
            RespondRaw(LoadRedirectFile($"/food/new?code={productId}"), System.Net.HttpStatusCode.Found);
        }
        [Method("POST"),   Path("/api/food/inventory")]
        public void NewInventory(string productId, string expires)
        {
            var split = expires.Split('-');
            var date = new DateTime(int.Parse(split[0]),
                                    int.Parse(split[1]),
                                    int.Parse(split[2]), 0, 0, 0, DateTimeKind.Utc);

            Service.AddInventoryItem(productId, "default", date);
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