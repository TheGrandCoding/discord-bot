using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            if (prod.Id != modal.ProductId)
            {
                var newProd = db.AddProduct(modal.ProductId, modal.Name, "");

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
                db.Products.Update(prod);
                await db.SaveChangesAsync();
            }
            await Context.Interaction.FollowupAsync($"Product information has been updated.", ephemeral: true);
        }
    
    }
}
