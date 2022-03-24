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

        void SendCurrent()
        {
            if(Recipe.NextStep == null)
            {
                sendDone();
            } else
            {
                var obj = Recipe.NextStep.ToShortJson();
                obj["started"] = Recipe.Started;
                if(Recipe.EstimatedEndAt.HasValue)
                {
                    obj["end"] = new DateTimeOffset(Recipe.EstimatedEndAt.Value).ToUnixTimeMilliseconds();
                }
                SendJson(obj);
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var jobj = JObject.Parse(e.Data);
            var data = jobj["data"].ToObject<string>();
            if(data == "next")
            {
                Recipe.NextStep.MarkStarted();
                Recipe.NextStep = Recipe.getNext();
                if (Recipe.NextStep == null)
                {
                    FoodService.OngoingRecipes.Remove(Recipe.Id, out _);
                }
                foreach (var x in Sessions.Sessions)
                {
                    if (x is FoodWS f)
                        f.SendCurrent();
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

            if(Recipe.NextStep == null)
            {
                Recipe.NextStep = Recipe.getNext();
            }
            SendCurrent();
        }
    }
}
