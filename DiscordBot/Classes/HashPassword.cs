﻿using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace DiscordBot.Classes
{
    public static class PasswordHash
    {
        public const int SaltByteSize = 24;
        public const int HashByteSize = 20; // to match the size of the PBKDF2-HMAC-SHA-1 hash 
        public const int Pbkdf2Iterations = 1000;
        public const int IterationIndex = 0;
        public const int SaltIndex = 1;
        public const int Pbkdf2Index = 2;

        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltByteSize);

            var hash = GetPbkdf2Bytes(password, salt, Pbkdf2Iterations, HashByteSize);
            return Pbkdf2Iterations + ":" +
                   Convert.ToBase64String(salt) + ":" +
                   Convert.ToBase64String(hash);
        }

        public static bool ValidatePassword(string password, string correctHash)
        {
            char[] delimiter = { ':' };
            var split = correctHash.Split(delimiter);
            var iterations = Int32.Parse(split[IterationIndex]);
            var salt = Convert.FromBase64String(split[SaltIndex]);
            var hash = Convert.FromBase64String(split[Pbkdf2Index]);

            var testHash = GetPbkdf2Bytes(password, salt, iterations, hash.Length);
            return SlowEquals(hash, testHash);
        }

        private static bool SlowEquals(byte[] a, byte[] b)
        {
            var diff = (uint)a.Length ^ (uint)b.Length;
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                diff |= (uint)(a[i] ^ b[i]);
            }
            return diff == 0;
        }

        private static byte[] GetPbkdf2Bytes(string password, byte[] salt, int iterations, int outputBytes)
        {
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt);
            pbkdf2.IterationCount = iterations;
            return pbkdf2.GetBytes(outputBytes);
        }


        public static string RandomToken(int length)
        {
            string token = "";
            while (token.Length < length)
            {
                int bottleFlip = Program.RND.Next(0, 3);
                // 0 = number (0-9)
                // 1 = upper (A-Z)
                // 2 = lower (a-z)
                if (bottleFlip == 0)
                {
                    token += Program.RND.Next(0, 10).ToString();
                }
                else if (bottleFlip == 1)
                {
                    int id = Program.RND.Next(65, 91);
                    char chr = Convert.ToChar(id);
                    token += chr.ToString();
                }
                else
                {
                    int id = Program.RND.Next(97, 123);
                    char chr = Convert.ToChar(id);
                    token += chr.ToString();
                }
            }
            return token;
        }
    }
}
