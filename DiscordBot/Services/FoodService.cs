using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Services
{
    public class FoodService : Service
    {

    }

    public class Product
    {
        public string Id { get; set; }
        public string Retailer { get; set; }
        public string Name { get; set; }
    }
    public class InventoryItem
    {
        public string Id { get; set; }

        public DateTime AddedAt { get; set; }   
        public DateTime ExpiresAt { get; set; }
    }
}
