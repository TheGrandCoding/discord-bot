﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Components
{
    public class FoodModule : InteractionModuleBase
    {
        public FoodService Service { get; set; }
        [ModalInteraction("food:edit:*")]
        public async Task EditProduct(string currentId, Modules.FoodProductModal modal)
        {
            await Context.Interaction.DeferAsync(ephemeral: true);
            using var db = Service.DB();
            var prod = db.GetProduct(currentId);
            if (prod == null)
            {
                await Context.Interaction.FollowupAsync($"No product exists by that ID", ephemeral: true);
                return;
            }
            int? extends = null;
            if (string.IsNullOrWhiteSpace(modal.Extends))
                extends = null;
            else if (int.TryParse(modal.Extends, out var e))
            {
                if(e > 0)
                    extends = e;
            }
            else
            {
                await Context.Interaction.FollowupAsync($"Extends must be empty or a valid integer");
            }

            if (prod.Id != modal.ProductId)
            {
                var newProd = db.AddProduct(modal.ProductId, modal.Name, "", extends);

                var inv = db.Inventory
                    .AsAsyncEnumerable()
                    .Where(x => x.ProductId == prod.Id);
                var invChanges = new List<InventoryItem>();
                await foreach (var x in inv)
                {
                    x.ProductId = newProd.Id;
                    invChanges.Add(x);
                }

                var his = db.PreviousInventory
                    .AsAsyncEnumerable()
                    .Where(x => x.ProductId == prod.Id);
                var hisChanges = new List<HistoricItem>();
                await foreach(var x in his)
                {
                    x.ProductId = newProd.Id;
                    hisChanges.Add(x);
                }


                db.Products.Remove(prod);
                db.Inventory.UpdateRange(invChanges);
                db.PreviousInventory.UpdateRange(hisChanges);
                await db.SaveChangesAsync();
            } else
            {
                prod.Name = modal.Name;
                prod.FreezingExtends = extends;
                db.Products.Update(prod);
                await db.SaveChangesAsync();
            }
            await Context.Interaction.FollowupAsync($"Product information has been updated.", ephemeral: true);
        }
    
        [ModalInteraction("food:inv:*")]
        public async Task EditInventoryItem(string currentId, Modules.FoodInventoryModal modal)
        {
            if(!DateTime.TryParseExact(modal.ExpiresAt, "yyyy-MM-dd", 
                Thread.CurrentThread.CurrentCulture, 
                System.Globalization.DateTimeStyles.AssumeUniversal, 
                out var expires))
            {
                await RespondAsync("Expiration date is not in a valid format.", ephemeral: true);
                return;
            }
            if(!bool.TryParse(modal.Frozen, out bool frozen))
            {
                await RespondAsync($"Frozen is not in a valid format.", ephemeral: true);
                return;
            }
            await DeferAsync(ephemeral: true);
            using var db = Service.DB();
            var inv = db.GetInventoryItem(int.Parse(currentId));
            inv.InitialExpiresAt = expires;
            inv.Frozen = frozen;
            db.Inventory.Update(inv);
            db.SaveChanges();
            await FollowupAsync($"Updated.", ephemeral: true);
        }

        [ModalInteraction("food:manu:edit:*")]
        public async Task Manufacturers(string oldName, Modules.FoodManuModal modal)
        {
            if(oldName != modal.ManuName)
            {
                Service.Manufacturers.Remove(oldName, out _);
            }
            Service.Manufacturers[modal.ManuName] = modal.Prefixes.Split(',').ToList();
            Service.OnSave();
            await RespondAsync("Updated.", ephemeral: true);
        }
    }
}
