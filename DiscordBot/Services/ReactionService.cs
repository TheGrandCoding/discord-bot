using Discord;
using DiscordBot.Classes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class ReactionService : SavedService
    {
        Dictionary<ulong, ReactionMessage> messages = new Dictionary<ulong, ReactionMessage>();

        public override string GenerateSave()
        {
            return Program.Serialise(messages);
        }

        public override void OnReady()
        {
            Program.Client.ReactionAdded += Client_ReactionAdded;
            Program.Client.ReactionRemoved += Client_ReactionRemoved;
        }

        public override void OnLoaded()
        {
            var content = ReadSave();
            messages = Program.Deserialise<Dictionary<ulong, ReactionMessage>>(content);
            var toRemove = new List<ulong>();
            foreach (var m in messages)
                if ((DateTime.Now - m.Value.Added).TotalHours > 36)
                    toRemove.Add(m.Key);
            foreach (var x in toRemove)
                messages.Remove(x);
        }

        private async Task handleItTho(Cacheable<IUserMessage, ulong> arg1, EventAction act, Discord.WebSocket.SocketReaction arg3)
        {
            var user = Program.Client.GetUser(arg3.UserId);
            if (user.IsBot)
                return;
            if (messages.TryGetValue(arg1.Id, out var msg))
            {
                if (msg.Events.HasFlag(act) == false)
                    return;
                var eventArgs = new ReactionEventArgs()
                {
                    Emote = arg3.Emote,
                    User = user,
                    Message = msg.Message,
                    State = msg.State,
                    Action = act
                };
                var method = msg.Method.GetMethod();
                method.Invoke(null, new object[] { this, eventArgs });
            }
        }

        private async Task Client_ReactionRemoved(Cacheable<IUserMessage, ulong> arg1, Discord.WebSocket.ISocketMessageChannel arg2, Discord.WebSocket.SocketReaction arg3)
        {
            await handleItTho(arg1, EventAction.Removed, arg3);
        }
        private async Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> arg1, Discord.WebSocket.ISocketMessageChannel arg2, Discord.WebSocket.SocketReaction arg3)
        {
            await handleItTho(arg1, EventAction.Added, arg3);
        }

        public void Register(IUserMessage message, EventAction subscribed, EventHandler<ReactionEventArgs> callback, string state = null)
        {
            var msg = new ReactionMessage()
            {
                Message = message,
                Method = new ReactionMethod(callback.Method),
                State = state,
                Added = DateTime.UtcNow,
                Events = subscribed
            };
            messages[message.Id] = msg;
        }
        public void Unregister(IUserMessage message)
        {
            messages.Remove(message.Id);
        }

        class ReactionMessage
        {
            [JsonConverter(typeof(DiscordConverter))]
            public IUserMessage Message { get; set; }
            public ReactionMethod Method { get; set; }
            public string State { get; set; }
            public DateTime Added { get; set; }
            public EventAction Events { get; set; }
        }
        class ReactionMethod
        {
            public string Class { get; set; }
            public string MethodName { get; set; }

            public MethodInfo GetMethod()
            {
                var type = Type.GetType(Class);
                return type.GetMethod(MethodName);
            }

            public ReactionMethod(MethodInfo info)
            {
                if (!info.IsPublic)
                    throw new ArgumentException("Method cannot be non-public");
                if (!info.IsStatic)
                    throw new ArgumentException("Method cannot be non-static");
                Class = info.DeclaringType.AssemblyQualifiedName;
                MethodName = info.Name;
            }
            [JsonConstructor]
            private ReactionMethod() { }
        }
    }

    public class ReactionEventArgs
    {
        public IUserMessage Message { get; set; }
        public IUser User { get; set; }
        public IEmote Emote { get; set; }
        public string State { get; set; }
        public EventAction Action { get; set; }
    }

    [Flags]
    public enum EventAction
    {
        Added   = 0b01,
        Removed = 0b10
    }
}
