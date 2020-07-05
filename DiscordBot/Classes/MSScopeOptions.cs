using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes
{
    public class MSScopeOptions
    {
        private List<string> scopes = new List<string>();

        string normalise(string name)
        {
            name = name.ToLower();
            if (name != "openid")
                name = "https://graph.microsoft.com/" + name.Replace("_", ".");
            return name;
        }

        bool get(string name)
        {
            return scopes.Contains(normalise(name));
        }

        void set(string name, bool value)
        {
            name = normalise(name);
            if(value)
            {
                if (!get(name))
                    scopes.Add(name);
            } else
            {
                scopes.Remove(name);
            }
        }

        public bool OpenId {

            get => get(nameof(OpenId));
            set => set(nameof(OpenId), value);
        }
        public bool User_Read
        {
            get => get(nameof(User_Read));
            set => set(nameof(User_Read), value);
        }
        public bool Team_ReadBasic_All
        {
            get => get(nameof(Team_ReadBasic_All));
            set => set(nameof(Team_ReadBasic_All), value);
        }

        public string GetScopes() => string.Join(" ", scopes);

        public MSScopeOptions(string[] properties)
        {
            foreach (var x in properties)
                set(x, true);
        }
        public MSScopeOptions() { }

        public override string ToString()
        {
            var properties = this.GetType().GetProperties()
                .Where(x => x.PropertyType == typeof(bool))
                .Where(x => get(x.Name));
            return string.Join(" ", properties.Select(x => x.Name));
        }

        public static implicit operator string(MSScopeOptions b) => b.GetScopes();
    }
}
