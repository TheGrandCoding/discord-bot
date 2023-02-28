using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class SelectMoveService : SavedService
    {
        public ConcurrentDictionary<ulong, Save> UserCycles { get; set; }
            = new ConcurrentDictionary<ulong, Save>();

        public override string GenerateSave()
            => Program.Serialise(UserCycles);

        public override void OnReady(IServiceProvider services)
        {
            var sv = ReadSave();
            var dict = Program.Deserialise<Dictionary<ulong, Save>>(sv);
            UserCycles = new ConcurrentDictionary<ulong, Save>(dict);
        }

        public override void OnDailyTick()
        {
            foreach((var userId, var save) in UserCycles)
            {
                save.Index = (save.Index + 1) % save.Cycle.Length;
                save.Update().Wait();
            }
            if(UserCycles.Count > 0)
                OnSave();
        }

        public class Save
        {
            public IUserMessage Message { get; set; }
            public string[] Cycle { get; set; }
            public int Index { get; set; }

            public override string ToString()
                => $"{Index} [{string.Join(", ", Cycle)}]";

            [JsonIgnore]
            public string Current => Cycle.Length == 0 ? null : Cycle[Index % Cycle.Length];
            [JsonIgnore]
            public string CustomId => $"selectmoveservice:{Message.Id}";

            public ComponentBuilder GetBuilder()
            {
                var builder = new ComponentBuilder();
                var select = new SelectMenuBuilder();
                select.WithCustomId(CustomId);
                for (int i = 0; i < Cycle.Length; i++)
                    select.AddOption(Cycle[i], i.ToString(), isDefault: i == Index);
                builder.WithSelectMenu(select);
                return builder;
            }
            
            public  Task Update()
            {
                return Message.ModifyAsync(x =>
                {
                    x.Content = $"Advanced to {Index} on {TimestampTag.FromDateTime(DateTime.Now)}";
                    x.Components = GetBuilder().Build();
                });
            }
        }

    }

}
