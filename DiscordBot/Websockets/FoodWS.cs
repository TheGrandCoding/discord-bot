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
        public WorkingRecipe Recipe { get; private set; }
        public FoodService FoodService { get; private set; }
        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            base.OnError(e);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var jobj = JObject.Parse(e.Data);
            var data = jobj["data"].ToObject<string>();
            if(data == "next")
            {
                Recipe.NextStep = Recipe.getNext();
                if(Recipe.NextStep == null)
                {
                    Sessions.Broadcast("{'data': 'done'}");
                } else
                {
                    Sessions.Broadcast(Recipe.NextStep.ToShortJson().ToString(Newtonsoft.Json.Formatting.Indented));
                }
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
            Recipe = FoodService.OngoingRecipes.FirstOrDefault(x => x.Id == id);
            if(Recipe == null)
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "Recipe not found");
                return;
            }

            if(Recipe.NextStep == null)
            {
                Recipe.NextStep = Recipe.getNext();
            }
            Send(Recipe.NextStep.ToShortJson());
        }
    }
}
