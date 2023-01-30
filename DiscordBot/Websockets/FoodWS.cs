using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSocketSharp;

namespace DiscordBot.Websockets
{
    public class FoodWS : BotWSBase
    {
        public WorkingRecipeCollection Recipe { get; private set; }
        public FoodService FoodService { get; private set; }
        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            base.OnError(e);
        }

        void sendDone()
        {
            var jobj = new JObject();
            jobj["done"] = "done";
            SendJson(jobj);
        }

        void SendRecipe()
        {
            var packet = new JObject();
            var json = Recipe.ToJson();
            packet["recipe"] = json;
            SendJson(packet);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var jobj = JObject.Parse(e.Data);
            Debug(jobj.ToString(), "OnMessage");
            if(jobj.TryGetValue("done", out _))
            {
                FoodService.OngoingRecipes.TryRemove(Recipe.Id, out _);
                this.Context.WebSocket.Close(CloseStatusCode.Normal, "Done");
            }
        }

        protected override void OnOpen()
        {
            if (!int.TryParse(Context.QueryString.Get("id"), out var id))
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "URL malformed.");
                return;
            }
            if (User == null)
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "Authentication failed.");
                return;
            }
            FoodService = Program.Services.GetRequiredService<FoodService>();
            if(!FoodService.OngoingRecipes.TryGetValue(id, out var v))
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "Recipe not found");
                return;
            }
            Recipe = v;

            SendRecipe();
        }
    }
}
