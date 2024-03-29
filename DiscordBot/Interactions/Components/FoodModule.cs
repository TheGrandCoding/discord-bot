﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
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
            int maxUses = 1;
            if(!int.TryParse(modal.Uses, out maxUses))
            {
                await Context.Interaction.FollowupAsync(":x: Uses was not a valid integer.", ephemeral: true);
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
                var newProd = db.AddProduct(modal.ProductId, modal.Name, "", extends, maxUses, modal.Tags);

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
                prod.Uses = maxUses;
                prod.FreezingExtends = extends;
                prod.Tags = modal.Tags;
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
            if(!int.TryParse(modal.TimesUsed, out int used))
            {
                await RespondAsync("Times used was not in a valid format.", ephemeral: true);
                return;
            }
            await DeferAsync(ephemeral: true);
            using var db = Service.DB();
            var inv = db.GetInventoryItem(int.Parse(currentId));
            inv.InitialExpiresAt = expires;
            inv.Frozen = frozen;
            inv.TimesUsed = used;
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
    
        [ComponentInteraction("food:scanned:*")]
        public async Task ScanSelect(string customId, string[] selected)
        {
            if(Service.DeleteInventoryItem(int.Parse(selected[0])))
            {
                if(Context.Interaction is SocketMessageComponent comp)
                {
                    await RespondAsync("Removed item.", ephemeral: true);
                    await comp.Message.DeleteAsync();
                }
            } else
            {
                await RespondAsync(":x: Could not remove item", ephemeral: true);
            }
        }

        [ComponentInteraction("food:notify:defrost")]
        public async Task HandledDefrost()
        {
            if(Context.Interaction is SocketMessageComponent comp)
            {
                if (Service.NotifyCancels.Remove(comp.Message.Id, out var src))
                {
                    src.Cancel();
                    await RespondAsync("Done, cleaning up.", ephemeral: true);
                    var messages = await Context.Channel.GetMessagesAsync(comp.Message.Id, Direction.After).FlattenAsync();
                    var bulk = new List<ulong>();
                    foreach (var after in messages)
                    {
                        if (after.Content.StartsWith("Defrost"))
                            bulk.Add(after.Id);
                    }
                    if (Context.Channel is ITextChannel txt)
                    {
                        await txt.DeleteMessagesAsync(bulk, new RequestOptions() { AuditLogReason = "Rem defrost notifs"});
                    } else
                    {
                        foreach (var id in bulk)
                            await Context.Channel.DeleteMessageAsync(id, new RequestOptions() { AuditLogReason = "Rem defrost notif" });
                    }
                    
                    await comp.Message.DeleteAsync();
                    await ModifyOriginalResponseAsync(x => x.Content = "Done.");
                }
            }
        }
    
        [ComponentInteraction("food:edit:*")]
        public async Task EditProductButton(string prodId)
        {
            var prod = Service.GetProduct(prodId);
            if (prod == null)
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
            mb.AddTextInput("Available uses", "uses", value: $"{prod.Uses}");
            mb.AddTextInput($"Tags", "tags", value: prod.Tags, required: false);

            await RespondWithModalAsync(mb.Build());
        }

        static HashSet<ulong> doneUses = new();
        [ComponentInteraction("food:uses:*")]
        public async Task FoodUsesButton(string kind)
        {
            if (Context.Interaction is not SocketMessageComponent comp)
            {
                await RespondAsync(":x: Internal error.", ephemeral: true);
                return;
            }
            if(doneUses.Contains(comp.Message.Id))
            {
                await RespondAsync(":x: Message already handled.", ephemeral: true);
                return;
            }
            var allEmbeds = comp.Message.Embeds.ToList();
            var data = allEmbeds.FirstOrDefault(x => x.Title.StartsWith("Confirm"));
            if (data == null)
            {
                await RespondAsync(":x: Could not find embed?", ephemeral: true);
                return;
            }
            await DeferAsync(ephemeral: true);
            if (kind == "refresh")
            {
                allEmbeds.Remove(data);
                using var scope = Program.GlobalServices.CreateScope();
                var yesE = Service.getYesterdayUsedItems(Service.GetWorkingMenu(scope.ServiceProvider)).Build();
                if(yesE != null)
                {
                    allEmbeds.Add(yesE);
                }
                else
                {
                    allEmbeds.Add(new EmbedBuilder().WithDescription("No yesterday items could be found.").Build());
                }
                await ModifyOriginalResponseAsync(x => x.Content = "Updated.");
                await comp.Message.ModifyAsync(x => x.Embeds = allEmbeds.ToArray());
                return;
            }

            var sb = new StringBuilder();
            foreach(var field in data.Fields)
            {
                var item = Service.GetInventoryItem(int.Parse(field.Name));
                if (item == null)
                {
                    sb.AppendLine($"- {field.Name} does not exist");
                    continue;
                }

                
                var uses = int.Parse(field.Value.Substring(0, field.Value.IndexOf('x')));

                if(!Service.AddUsesInventoryItem(int.Parse(field.Name), uses, comp.Message.CreatedAt.UtcDateTime.AddDays(-1)))
                {
                    sb.AppendLine($"- Could not add {uses} uses to {field.Name}, {field.Value}");
                } else
                {
                    sb.AppendLine($"v Added {uses} for {field.Name}");
                }
            }
            doneUses.Add(comp.Message.Id);

            await ModifyOriginalResponseAsync(x => x.Content = sb.ToString());
            await comp.Message.DeleteAsync();
        }
    }
}
