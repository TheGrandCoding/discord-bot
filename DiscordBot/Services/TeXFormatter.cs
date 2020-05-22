﻿using Discord;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace DiscordBot.Services
{
    public class TeXFormatter : Service
    {
        HttpClient Client { get; set; }
        const string API = "https://en.wikipedia.org/api/rest_v1";

        public override void OnReady()
        {
            Client = Program.Services.GetRequiredService<HttpClient>();
            Program.Client.MessageReceived += Client_MessageReceived;
        }

        string[] MATH_SYMBOLS = new string[]
        {
            "+", "-", "*", "/", "{", "}", "\\over", "\\sqrt", "^"
        };
        Dictionary<string, string> REPLACEMENTS = new Dictionary<string, string>()
        {
            {"*", " \\times " }
        };

        bool shouldRespond(string msg)
        {
            if (msg.StartsWith('.'))
                return true;
            foreach (var s in MATH_SYMBOLS)
                if (msg.Contains(s))
                    return true;
            return false;
        }

        private async System.Threading.Tasks.Task Client_MessageReceived(Discord.WebSocket.SocketMessage arg)
        {
            if (arg.Channel.Name != "maths")
                return;
            string message = arg.Content;
            if (!shouldRespond(message))
                return;
            var json = new JObject();
            message = message[0] == '.' ? message.Substring(1) : message;
            foreach (var keypair in REPLACEMENTS)
                message = message.Replace(keypair.Key, keypair.Value);
            json["q"] = message;
            var jsonS = json.ToString(Newtonsoft.Json.Formatting.None);
            var sendContent = new StringContent(jsonS, Encoding.UTF8, "application/json");
            var response = await Client.PostAsync($"{API}/media/math/check/tex", sendContent);
            var content = await response.Content.ReadAsStringAsync();
            var jResponse = JObject.Parse(content);
            if(!response.IsSuccessStatusCode)
            {
                string detail = jResponse["detail"]?.ToString() ?? $"No information gathered.";
                if (detail.Length >= 1024)
                    detail = $"Detailed information was too long to be included";
                await arg.Channel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithTitle("LaTeX Rendering Failed")
                    .WithDescription($"Could not render the below:\r\n```\r\n{json["q"]}\r\n```")
                    .AddField("Title", jResponse["title"]?.ToString() ?? $"{response.StatusCode} {response.ReasonPhrase}")
                    .AddField("Detail", detail)
                    .WithColor(Discord.Color.Red)
                    .WithUrl(arg.GetJumpUrl())
                    .Build());
                return;
            }
            string hash = response.Headers.GetValues("x-resource-location").First();
            var imageUrl = $"{API}/media/math/render/png/" + hash;
            string formula = $"```\r\n{message}\r\n```";
            if (formula.Length > 1024)
                formula = $"[From this message]({arg.GetJumpUrl()})";
            await arg.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithTitle("LaTeX Rendered")
                .WithDescription(formula)
                .WithFooter(hash)
                .WithImageUrl(imageUrl)
                .WithColor(Discord.Color.Green)
                .WithUrl(imageUrl)
                .Build());
        }
    }
}
