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
            if(Recipe.StartedAt.HasValue)
                packet["startedAt"] = Recipe.StartedAt.Value;
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
            } else if(jobj.TryGetValue("data", out var dataT) && dataT is JObject data)
            {
                if(data.TryGetValue("mute", out var catalystT))
                {
                    var catalyst = catalystT.ToObject<string>();
                    var cat = Recipe.RecipeGroups.FirstOrDefault(x => x.Catalyst == catalyst);
                    if(cat != null)
                    {
                        cat.Muted = true;
                    }
                } else if (data.TryGetValue("advance", out var payloadT) && payloadT is JObject payload)
                {
                    var catalyst = payload["id"].ToObject<string>();
                    var started = payload["started"].ToObject<ulong>();
                    var time = payload["time"].ToObject<ulong>();
                    if (!Recipe.StartedAt.HasValue)
                        Recipe.StartedAt = started;
                    var cat = Recipe.RecipeGroups.FirstOrDefault(x => x.Catalyst == catalyst);
                    if(cat != null)
                    {
                        cat.Muted = null;
                        cat.Alarm = false;
                        cat.AdvancedAt = time;
                        cat.StartedAt = started;

                        for(int i = 0; i < cat.SimpleSteps.Count; i++)
                        {
                            var step = cat.SimpleSteps[i];
                            if (step.State == WorkingState.Complete) continue;
                            if(step.State == WorkingState.Pending)
                            {
                                step.State = WorkingState.Ongoing;
                                break;
                            }
                            if(step.State == WorkingState.Ongoing)
                            {
                                step.State = WorkingState.Complete;
                                var next = cat.SimpleSteps.ElementAtOrDefault(i + 1);
                                if(next != null)
                                {
                                    next.State = WorkingState.Ongoing;
                                    Info($"Moving to {next.Text} in {step.Duration}");
                                    if (next.Duration > 0) break;
                                } else
                                {
                                    Info($"Finished group with final {step.Text}");
                                }
                            }
                        }
                    }
                }
            }
            SendToAllOthers(jobj);
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
            Recipe.Initialise();

            SendRecipe();
        }
    }
}
