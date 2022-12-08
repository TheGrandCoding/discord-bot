using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DiscordBot.Services
{
    public abstract class EncryptedService : SavedService
    {
        public override string SaveFile => $"{Name}.encrypted";
        string encrypt(byte[] key, string plainText)
        {
            byte[] iv = new byte[16];
            byte[] array;
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
                        {
                            streamWriter.Write(plainText);
                        }
                        array = memoryStream.ToArray();
                    }
                }
            }
            return Convert.ToBase64String(array);
        }

        string decrypt(byte[] key, string cipherText)
        {
            byte[] iv = new byte[16];
            byte[] buffer = Convert.FromBase64String(cipherText);
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using (MemoryStream memoryStream = new MemoryStream(buffer))
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader streamReader = new StreamReader((Stream)cryptoStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }
    
        public static byte[] GetRandomKey()
        {
            return RandomNumberGenerator.GetBytes(16);
        }

        protected abstract string KeyLocation { get; }

        byte[] getKey()
        {
            var path = Path.Combine(Program.BASE_PATH, "data", "keys");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path = Path.Combine(path, KeyLocation + ".key");
            byte[] buffer;
            try
            {
                buffer = Convert.FromBase64String(File.ReadAllText(path));
            } catch (FileNotFoundException)
            {
                buffer = GetRandomKey();
                File.WriteAllText(path, Convert.ToBase64String(buffer));
            }
            return buffer;
        }

        public override void OnSave()
        {
            var plainText = GenerateSave();
            if (string.IsNullOrWhiteSpace(plainText))
                return;
            var key = getKey();
            var content = encrypt(key, plainText);
            if (!Directory.Exists(SaveFolder))
                Directory.CreateDirectory(SaveFolder);
            File.WriteAllText(Path.Combine(SaveFolder, SaveFile), content);
        }
        public override string ReadSave(string defaultContent = "{}")
        {
            var cipherText = base.ReadSave(null);
            if (cipherText == null)
                return defaultContent;
            var key = getKey();
            var plainText = decrypt(key, cipherText);
            return plainText;
        }
    }
}
