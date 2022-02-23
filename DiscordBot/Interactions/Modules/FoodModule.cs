﻿using Discord;
using Discord.Interactions;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Modules
{
    [Group("food", "Commands for stuff")]
    [DontAutoRegister]
    public class FoodModule : BotSlashBase
    {
        public FoodService Service { get; set; }
        [SlashCommand("product", "Edit a products")]
        public async Task EditProduct(string prodId)
        {
            var prod = Service.GetProduct(prodId);
            if(prod == null)
            {
                await RespondAsync("No product by that ID.", ephemeral: true);
                return;
            }
            var mb = new ModalBuilder();
            mb.WithTitle("Edit Product");
            mb.WithCustomId("food:edit:" + prodId);
            mb.AddTextInput("Product Id", "product_id", value: prodId, minLength: 8, maxLength: 13, required: true);
            mb.AddTextInput("Name", "name", value: prod.Name);

            await RespondWithModalAsync(mb.Build());
        }
    
        [SlashCommand("purge", "Removes all products")]
        [RequireOwner]
        public async Task PurgeProducts()
        {
            await DeferAsync(true);
            using var db = Service.DB();
            db.Products.RemoveRange(db.Products);
            db.Inventory.RemoveRange(db.Inventory);
            db.PreviousInventory.RemoveRange(db.PreviousInventory);
            await db.SaveChangesAsync();
            await FollowupAsync("Done!");
        }
    }

    public class FoodProductModal : IModal
    {
        public string Title => "Edit Product";

        [InputLabel("Product ID")]
        [ModalTextInput("product_id", minLength: 6, maxLength: 8)]
        public string ProductId { get; set; }

        [InputLabel("Name")]
        [ModalTextInput("name")]
        public string Name { get; set; }
    }
}
