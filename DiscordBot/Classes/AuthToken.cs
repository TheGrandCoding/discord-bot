using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using static DiscordBot.Program;

namespace DiscordBot.Classes
{
    public class AuthToken
    {
        /// <summary>
        /// Password used to login
        /// </summary>
        public const string LoginPassword = "htmlbypasspwd";
        /// <summary>
        /// Token used to identify a unique session
        /// </summary>
        public const string SessionToken = "session";

        public string Name { get; set; }

        [JsonProperty("Value")]
        string _value;

        [JsonIgnore]
        public string Value {  get
            {
                return _value;
            }  [Obsolete]set
            {
                _value = value;
            }
        }

        [JsonConstructor]
        public AuthToken(string name, string value)
        {
            Name = name;
            _value = value;
        }

        const int defaultLength = 12;
        public AuthToken(string name, int length = defaultLength) : this(name, Generate(length))
        {
        }

        public void SetHashValue(string plainValue)
        {
            _value = PasswordHash.HashPassword(plainValue);
        }
        public bool SamePassword(string plainTest) => PasswordHash.ValidatePassword(plainTest, _value);

        public void Regenerate(int length = -1)
        {
            if (length < 0)
                length = Value?.Length ?? defaultLength;
            _value = Generate(length);
        }

        public static string Generate(int length)
        {
            string token = "";
            while (token.Length < length)
            {
                int bottleFlip = RND.Next(0, 3);
                // 0 = number (0-9)
                // 1 = upper (A-Z)
                // 2 = lower (a-z)
                if (bottleFlip == 0)
                {
                    token += RND.Next(0, 10).ToString();
                }
                else if (bottleFlip == 1)
                {
                    int id = RND.Next(65, 91);
                    char chr = Convert.ToChar(id);
                    token += chr.ToString();
                }
                else
                {
                    int id = RND.Next(97, 123);
                    char chr = Convert.ToChar(id);
                    token += chr.ToString();
                }
            }
            return token;
        }
    }
}
