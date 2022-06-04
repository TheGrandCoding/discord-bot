using Discord;
using Discord.Interactions;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Modules
{
    [Group("food", "Commands for stuff")]
    [RequireOwner]
    [DontAutoRegister]
    public class FoodModule : BotSlashBase
    {
        public FoodService Service { get; set; }
        [SlashCommand("product", "Edit a products")]
        public async Task EditProduct(string prodId)
        {
            prodId = prodId.Replace(" ", "");
            var prod = Service.GetProduct(prodId);
            if(prod == null)
            {
                await RespondAsync("No product by that ID.", ephemeral: true);
                return;
            }
            var mb = new ModalBuilder();
            mb.WithTitle("Edit Product");
            mb.WithCustomId("food:edit:" + prodId);
            mb.AddTextInput("Product Id", "product_id", value: prodId, minLength: 4, maxLength: 16, required: true);
            mb.AddTextInput("Name", "name", value: prod.Name);
            mb.AddTextInput($"Days Freezing Extends Expiry", "extends", value: $"{(prod.FreezingExtends ?? 0)}");
            mb.AddTextInput($"Tags", "tags", value: prod.Tags, required: false);

            await RespondWithModalAsync(mb.Build());
        }
    
        [SlashCommand("inventory", "Edit an inventory item")]
        public async Task EditInventoryItem(int invItem)
        {
            var inv = Service.GetInventoryItem(invItem);
            if(inv == null)
            {
                await RespondAsync("No item by that ID.", ephemeral: true);
                return;
            }
            var mb = new ModalBuilder();
            mb.WithCustomId("food:inv:" + invItem.ToString());
            mb.WithTitle("Edit Inventory Item");
            mb.AddTextInput("Expires At", "expires", placeholder: "yyyy-MM-dd", 
                maxLength: 10, required: true, value: $"{inv.InitialExpiresAt:yyyy-MM-dd}");
            mb.AddTextInput("Frozen", "frozen", placeholder: "true/false", minLength: 4, maxLength: 5, required: true, value: $"{inv.Frozen}");

            await RespondWithModalAsync(mb.Build());
        }

        [SlashCommand("purge", "Removes all products")]
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
    
        [SlashCommand("manufacturor", "View and edit manufactorer prefixes")]
        public async Task Manufacturor(string name)
        {
            if (!Service.Manufacturers.TryGetValue(name, out var ls))
                ls = new List<string>();

            var mb = new ModalBuilder()
            {
                Title = "Edit Manufacturer",
                CustomId = "food:manu:edit:" + name
            };
            mb.AddTextInput("Manufacturer Name", "manu_name", required: true, value: name);
            mb.AddTextInput("Prefix", "prefix", placeholder: "prefix1,prefix2,prefix3", value: string.Join(",", ls));

            await RespondWithModalAsync(mb.Build());
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

        [ModalTextInput("extends")]
        public string Extends { get; set; }

        [ModalTextInput("tags")]
        public string Tags { get; set; }
    }

    public class FoodInventoryModal : IModal
    {
        public string Title => "Edit Inventory Item";

        [ModalTextInput("expires", placeholder: "yyyy-MM-dd", maxLength: 10)]
        public string ExpiresAt { get; set; }

        [ModalTextInput("frozen", placeholder: "true/false", maxLength: 5, minLength: 4)]
        public string Frozen { get; set; }
    }

    public class FoodManuModal : IModal
    {
        public string Title => "";

        [ModalTextInput("manu_name")]
        public string ManuName { get; set; }
        [ModalTextInput("prefix")]
        public string Prefixes { get; set; }
    }
}
