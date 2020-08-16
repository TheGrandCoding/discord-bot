using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DiscordBot
{
    public static class Hash
    {
        public static string GetSHA1(string plain)
        {
            using(var sha = new SHA1Managed())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(plain));
                return string.Concat(hash.Select(b => b.ToString("X2")));
            }
        }
        public static string GetPbkdf2(string plain) => PasswordHash.HashPassword(plain);
    }
}
