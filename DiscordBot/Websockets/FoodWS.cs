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

        void sendDone()
        {
            var jobj = new JObject();
            jobj["done"] = "done";
            SendJson(jobj);
        }

        void SendSteps()
        {
            if(Recipe.OnScreenNow == null)
            {
                sendDone();
            } else
            {
                var packet = new JObject();
                packet["started"] = Recipe.Started;
                if (Recipe.EstimatedEndAt.HasValue)
                {
                    packet["end"] = new DateTimeOffset(Recipe.EstimatedEndAt.Value).ToUnixTimeMilliseconds();
                }
                packet["current"] = Recipe.OnScreenNow.ToShortJson();
                packet["next"] = Recipe.Steps.Next?.ToShortJson() ?? null;
                SendJson(packet);
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var jobj = JObject.Parse(e.Data);
            Debug(jobj.ToString(), "OnMessage");
            var data = jobj["data"].ToObject<string>();
            if(data == "next")
            {
                Recipe.Steps.MarkStarted();
                Recipe.OnScreenNow = Recipe.Steps.Current;
                if (Recipe.OnScreenNow == null)
                {
                    FoodService.OngoingRecipes.Remove(Recipe.Id, out _);
                }
                foreach (var x in Sessions.Sessions)
                {
                    if (x is FoodWS f)
                        f.SendSteps();
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
            if(!FoodService.OngoingRecipes.TryGetValue(id, out var v))
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "Recipe not found");
                return;
            }
            Recipe = v;

            if(Recipe.OnScreenNow == null)
            {
                Recipe.OnScreenNow = Recipe.Steps.Current;
            }
            SendSteps();
        }
    }
}
