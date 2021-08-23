using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace DiscordBot.Classes
{
    /// <summary>
    /// Class used to hold information about a login session on the bot's website.
    /// </summary>
    public class AuthSession
    {
        public const string CookieName = "session";
        [JsonConstructor]
        private AuthSession()
        {
        }

        public AuthSession(string ip, string ua, bool approved)
        {
            Started = DateTime.Now;
            IpAddress = ip;
            UserAgent = ua;
            Approved = approved;
            Token = AuthToken.Generate(32);
        }
        /// <summary>
        /// The date and time of login
        /// </summary>
        public DateTime Started { get; set; }

        /// <summary>
        /// The IP address of login
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// The user agent of login
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// Whether this session is approved. New IPs require approval via DM.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Approved { get; set; } = false;

        /// <summary>
        /// The token stored in the cookie header
        /// </summary>
        public string Token { get; set; }

    }
}
