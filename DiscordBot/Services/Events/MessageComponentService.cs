using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    /*public class MessageComponentService : SavedService
    {
        public MessageComponentService()
        {
            Warning($"Created message component service", "cntr");
        }
        Dictionary<string, CallbackMessage> messages { get; set; } = new Dictionary<string, CallbackMessage>();

        public override string GenerateSave()
        {
            return Program.Serialise(messages);
        }

        public override void OnLoaded()
        {
            var sv = ReadSave();
            messages = Program.Deserialise<Dictionary<string, CallbackMessage>>(sv);
            
        }

        public delegate Task InteractionEvent(CallbackEventArgs e);

        public void Register(string customId, IUserMessage message, InteractionEvent callback, string state = null, bool doSave = true)
        {
            Debug($"Registering callback {callback.Method.Name} for {customId}\r\n" +
                $"Current callbacks: \r\n" +
                $"- " + string.Join("\r\n- ", messages.Keys));
            var msg = new CallbackMessage()
            {
                Message = message,
                State = state,
                Added = DateTime.UtcNow,
                Save = doSave
            };
            if (doSave)
            {
                msg.Method = new CallbackMethod(callback.Method);
            }
            else
            {
                msg.Method = new CallbackMethod(callback);
            }
            messages[customId] = msg;
            if (doSave)
                OnSave();
        }
        public void Unregister(string customId)
        {
            messages.Remove(customId);
            Debug($"Unregistered {customId}, {messages.Count} remain");
        }

        public void Register(IUserMessage message, InteractionEvent callback, string state = null, bool doSave = true)
            => Register(message.Id.ToString(), message, callback, state, doSave);
        public void Unregister(IUserMessage message) => Unregister(message.Id.ToString());

        public async Task<IResult> ExecuteAsync(SocketMessageComponent arg)
        {
            if(!messages.TryGetValue($"{arg.Message?.Id ?? 0}", out var data))
                if(!messages.TryGetValue(arg.Data.CustomId, out data))
                    return ExecuteResult.FromError(CommandError.Unsuccessful, $"No state data found, unknown callback ({arg.Message?.Id ?? 0}, {arg.Data.CustomId})");

            var args = new CallbackEventArgs()
            {
                ComponentId = arg.Data.CustomId,
                Message = arg.Message,
                State = data.State,
                User = arg.User,
                Interaction = arg
            };
            try
            {
                var result = await data.Method.Invoke(args).ConfigureAwait(false);
                return result;
            } catch(Exception ex)
            {
                return ExecuteResult.FromError(ex);
            }
        }
    }

    class CallbackMessage
    {
        [JsonConverter(typeof(DiscordConverter))]
        public IMessage Message { get; set; }
        public CallbackMethod Method { get; set; }
        public string State { get; set; }
        public DateTime Added { get; set; }
        public bool Save { get; set; }
    }

    class CallbackMethod
    {
        public string Class { get; set; }
        public string MethodName { get; set; }

        Delegate _eh;

        MethodInfo GetMethod()
        {
            var type = Type.GetType(Class);
            return type.GetMethod(MethodName);
        }

        public async Task<IResult> Invoke(CallbackEventArgs args)
        {
            Task task;
            if (_eh != null)
            {
                task = _eh.DynamicInvoke(new object[] { args }) as Task ?? Task.Delay(0);
            }
            else
            {
                task = GetMethod().Invoke(null, new object[] { args }) as Task ?? Task.Delay(0);
            }

            if (task is Task<RuntimeResult> resultTask)
            {
                return await resultTask.ConfigureAwait(false);
            }
            else
            {
                await task.ConfigureAwait(false);
                return ExecuteResult.FromSuccess();
            }
        }

        public CallbackMethod(MethodInfo info)
        {
            if (!info.IsPublic)
                throw new ArgumentException("Method cannot be non-public");
            if (!info.IsStatic)
                throw new ArgumentException("Method cannot be non-static");
            Class = info.DeclaringType.AssemblyQualifiedName;
            MethodName = info.Name;
        }

        public CallbackMethod(Delegate handler)
        {
            _eh = handler;
            Class = handler.Method.DeclaringType.AssemblyQualifiedName;
            MethodName = handler.Method.Name;
        }

        [JsonConstructor]
        private CallbackMethod() { }
    }

    public class CallbackEventArgs
    {
        public IUserMessage Message { get; set; }
        public IUser User { get; set; }
        public string State { get; set; }
        public string ComponentId { get; set; }
        public SocketMessageComponent Interaction { get; set; }
    }*/
}
