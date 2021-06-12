using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Classes.Rules;
using DiscordBot.MLAPI;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace DiscordBot.Websockets
{
    public class BanAppealsWS : WebSocketBehavior
    {
        public BanAppealsService Service { get; set; }
        public MsgService MService { get; set; }
        public BanAppeal Appeal { get; set; }
        public BotUser User { get; set; }
        protected override void OnOpen()
        {
            if(!ulong.TryParse(Context.QueryString.Get("id"), out var id))
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "URL malformed.");
                return;
            }
            if(!Handler.findToken(Context.CookieCollection[AuthToken.SessionToken].Value, out var usr, out _))
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "Authentication failed.");
                return;
            }
            User = usr;
            var guild = Program.Client.GetGuild(id);
            Service = Program.Services.GetRequiredService<BanAppealsService>();
            MService = Program.Services.GetRequiredService<MsgService>();
            Appeal = Service.GetAppeal(guild, User.Id);
            if(Appeal == null)
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "404 Ban Appeal Missing");
                return;
            }
            if(Appeal.IsMuted)
            {
                SendInfo(muted: Program.FormatTimeSpan(Appeal.MutedUntil.Value - DateTime.Now, true));
            }
            Program.Client.MessageReceived += filterMsg;
            Program.Client.MessageUpdated += Client_MessageUpdated;
        }

        public void SendInfo(string error = null, string muted = null)
        {
            var jobj = new JObject();
            if(error != null)
                jobj["error"] = error;
            if (muted != null)
                jobj["muted"] = muted;
            Send(jobj.ToString(Newtonsoft.Json.Formatting.None));
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Program.Client.MessageReceived -= filterMsg;
        }
        protected override void OnMessage(MessageEventArgs e)
        {
            Program.LogVerbose(e.Data, "AppealsMsg");
            var json = JObject.Parse(e.Data);
            var type = json["type"].ToString();
            if(type == "GetMessages")
            {
                var before = json["before"].ToObject<long>();
                var msgId = SnowflakeUtils.ToSnowflake(DateTimeOffset.FromUnixTimeMilliseconds(before));
                var messages = Appeal.AppealChannel.GetMessagesAsync(msgId, Direction.Before).FlattenAsync().Result;
                foreach (var x in messages)
                    if(x is IUserMessage u)
                        NewMessage(u);
            } else if (type == "SendMessage")
            {
                if(Appeal.IsMuted)
                {
                    var time = Program.FormatTimeSpan(Appeal.MutedUntil.Value - DateTime.Now, true);
                    SendInfo("Error: This ban appeal is muted.", time);
                } else
                {
                    var content = json["content"].ToString();
                    try
                    {
                        Appeal.SendMessageAsync(content, User.Name, User.GetAvatarUrl()).Wait();
                    } catch(Exception ex)
                    {
                        Program.LogError(ex, "BanAppeal");
                        SendInfo($"Exception occured: {ex.Message}");
                    }
                }
            }
        }

        async Task Client_MessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        {
            await filterMsg(arg2);
        }
        async Task filterMsg(SocketMessage arg)
        {
            if(arg is IUserMessage umsg 
                && arg.Channel is ITextChannel channel
                && channel.GuildId == Appeal.Guild.Id)
            {
                NewMessage(umsg);
            }
        }

        string getAuthorName(DiscordMsg msg)
        {
            if (msg.Author.Id == User.Id)
                return User.Name;
            if (msg.Author.IsWebhook || msg.Author.IsBot)
                return msg.Author.Username;
            return "[Administrator]";
        }
        string getRoleColour(DiscordMsg msg)
        {
            if (msg.Author.Id == User.Id)
                return "blue";
            if (msg.Author.IsWebhook)
                return "blue";
            if (msg.Author.IsBot)
                return "orange";
            return "red";
        }

        void NewMessage(IUserMessage message)
        {
            if(message.Author.Id == Program.Client.CurrentUser.Id)
            {
                if(message.Embeds.Count > 0 && message.Embeds.First().Color == Color.Red)
                {
                    return;
                }
            }
            if (message.Content.StartsWith("$") || message.Content.StartsWith("^"))
                return;
            var json = new JObject();
            json["id"] = message.Id.ToString();
            var dMsg = new DiscordMsg(MService, message);
            var msg = DiscordBot.MLAPI.Modules.VPN.getMessage(dMsg, 
                Appeal.Guild, getAuthorName(dMsg), getRoleColour(dMsg), User.Id, false, false);
            json["html"] = msg.ToString();
            json["author"] = (dMsg.Author.IsWebhook ? User.Id : dMsg.Author.Id).ToString();
            Send(json.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
}
