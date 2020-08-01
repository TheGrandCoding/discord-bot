using DiscordBot.Classes;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class OauthCallbackService : Service
    {
        public Dictionary<string, OauthCallback> States { get; set; } = new Dictionary<string, OauthCallback>();

        public string Register(EventHandler<object[]> callback, params object[] args)
        {
            var type = callback.Target.GetType();
            if (!type.IsSubclassOf(typeof(APIBase)))
                throw new ArgumentException("Callback must be on APIBase!");
            var rnd = AuthToken.Generate(32);
            States[rnd] = new OauthCallback()
            {
                Arguments = args,
                Handler = callback
            };
            return rnd;
        }

        public bool Invoke(APIContext context, string state)
        {
            if (!States.Remove(state, out var obj))
                return false;

            var type = obj.Handler.Target.GetType();
            var cnt = System.Activator.CreateInstance(type, context);
            obj.Handler.Method.Invoke(cnt, new object[] { context, obj.Arguments });
            return true;
        }
    }
    public class OauthCallback
    {
        public object[] Arguments { get; set; }
        public EventHandler<object[]> Handler { get; set; }
    }
}
