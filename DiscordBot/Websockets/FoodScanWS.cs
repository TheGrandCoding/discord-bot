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
    public class FoodScanWS : BotWSBase
    {
        public FoodService FoodService { get; private set; }
        public void SendCode(string code)
        {
            var data = new JObject();
            data["code"] = code;
            this.SendJson(data);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            base.OnError(e);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
        }

        protected override void OnOpen()
        {
        }
    }
}
