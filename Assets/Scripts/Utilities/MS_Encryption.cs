using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Xml.Serialization;
using UnityEngine;

namespace Game.Utilities
{
    /// <summary> 
    /// A class for storing Data about a class. 
    /// main method using Microsoft C# Cryptography
    /// </summary>
    [System.Serializable]
    public class ES_Data<T>
    {
        public T SaveData;
        public ES_Data() { }
        public ES_Data(T Data)
        { SaveData = Data; }
    }

    /// <summary> The class that provides encryption. </summary>
    public static class MS_Encryption
    {
        private static readonly string keyString = "88 197 229 184 239 134 141 215 45 144 105 205 142 21 41 234 169 212 60 11 10 202 24 136 104 237 120 54 156 185 63 125";
        private static readonly byte[] key = GetBytes(keyString);
        private static AesCryptoServiceProvider aes = new AesCryptoServiceProvider();

        // Taken from https://stackoverflow.com/questions/2434534/serialize-an-object-to-string
        public static T Deserialize<T>(this string toDeserialize)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
            using (StringReader textReader = new StringReader(toDeserialize))
            {
                return (T)xmlSerializer.Deserialize(textReader);
            }
        }
        public static string Serialize<T>(this T toSerialize)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
            using (StringWriter textWriter = new StringWriter())
            {
                xmlSerializer.Serialize(textWriter, toSerialize);
                return textWriter.ToString();
            }
        }

        // Taken from https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.aescryptoserviceprovider?view=netframework-4.7.2
        public static byte[] EncryptStringToBytes_Aes(string plainText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (plainText == null || plainText.Length <= 0)
                throw new System.ArgumentNullException(nameof(plainText));
            if (Key == null || Key.Length <= 0)
                throw new System.ArgumentNullException(nameof(Key));
            if (IV == null || IV.Length <= 0)
                throw new System.ArgumentNullException(nameof(IV));
            byte[] encrypted;

            // Create an AesCryptoServiceProvider object
            // with the specified key and IV.
            using (AesCryptoServiceProvider aesAlg = new AesCryptoServiceProvider())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create an encryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            //Write all data to the stream.
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }

            // Return the encrypted bytes from the memory stream.
            return encrypted;

        }
        public static string DecryptStringFromBytes_Aes(byte[] cipherText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (cipherText == null || cipherText.Length <= 0)
                throw new System.ArgumentNullException(nameof(cipherText));
            if (Key == null || Key.Length <= 0)
                throw new System.ArgumentNullException(nameof(Key));
            if (IV == null || IV.Length <= 0)
                throw new System.ArgumentNullException(nameof(IV));

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            // Create an AesCryptoServiceProvider object
            // with the specified key and IV.
            using (AesCryptoServiceProvider aesAlg = new AesCryptoServiceProvider())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create a decryptor to perform the stream transform.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }

            }

            return plaintext;

        }

        public static string FixDecrypted(string pDecrypted)
        {
            int len = pDecrypted.Length;
            int start = 0;
            for (int i = 0; i < len; ++i)
            {
                if (pDecrypted.Substring(i, 3) == ".0\"") { start = i + 3; break; }
                if (i > 30) break;
            }
            pDecrypted = "<?xml version=\"1.0\"" + pDecrypted.Substring(start);
            return pDecrypted;
        }

        public static byte[] GetBytes(string pData)
        {
            string[] encrypted = pData.Split(char.Parse(" "));
            byte[] bytes = new byte[encrypted.Length];
            int len = encrypted.Length;

            for (int i = 0; i < len; ++i)
            { bytes[i] = byte.Parse(encrypted[i]); }
            return bytes;
        }
        public static string GetString(byte[] pData)
        {
            string str = "";
            int len = pData.Length;
            for (int i = 0; i < len; ++i)
            {
                str += "" + pData[i];
                if (i < len - 1) str += " ";
            }
            return str;
        }

        /// <summary> Saves Data into encrypted memory. </summary>
        /// <param name="pData">The Data to save, such as 'Albert', or 0, 1.5, false, and so on.</param>
        /// <param name="pPath">The Path to save Data into, such as 'Player Data/Albert'.</param>
        public static void SaveFile<T>(T pData, string pPath, string encryptKey = "")
        {
            string dataPath = Application.persistentDataPath + "/" + pPath + ".data";
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream stream = new FileStream(dataPath, FileMode.Create);

            ES_Data<T> saveData = new ES_Data<T>(pData);
            string serialized = Serialize(saveData);
            byte[] bytes;
            aes.Key = (encryptKey == "") ? key : GetBytes(encryptKey);
            aes.GenerateIV();
            bytes = EncryptStringToBytes_Aes(serialized, aes.Key, aes.IV);
            formatter.Serialize(stream, bytes);

            stream.Close();
        }
        /// <summary> Saves Data into encrypted memory. </summary>
        /// <param name="pData">The Data to save, such as 'Albert', or 0, 1.5, false, and so on.</param>
        /// <param name="pKey">The key to save Data into, such as 'Albert'.</param>
        public static void SavePref<T>(T pData, string pKey)
        {
            ES_Data<T> saveData = new ES_Data<T>(pData);
            string serialized = Serialize(saveData);
            byte[] bytes;
            aes.Key = key;
            aes.GenerateIV();
            bytes = EncryptStringToBytes_Aes(serialized, aes.Key, aes.IV);
            string encrypted = GetString(bytes);
            PlayerPrefs.SetString(pKey, encrypted);
            PlayerPrefs.Save();
        }

        public static string EncryptString(string data, string encryptKey = "")
        {
            byte[] bytes;
            aes.Key = (encryptKey == "") ? key: GetBytes(encryptKey);
            aes.GenerateIV();
            bytes = EncryptStringToBytes_Aes(data, aes.Key, aes.IV);
            byte[] toSave = aes.IV.Concat(bytes).ToArray();
            return Convert.ToBase64String(toSave);
        }

        public static string DescryptString(string data, string encryptKey = "")
        {
            byte[] bytes = Convert.FromBase64String(data);
            aes.Key = (encryptKey == "") ? key : GetBytes(encryptKey);
            aes.IV = bytes.Take(16).ToArray();
            return DecryptStringFromBytes_Aes(bytes.Skip(16).ToArray(), aes.Key, aes.IV);
        }

        /// <summary> Returns a Data object from encrypted memory, if it exists. </summary>
        /// <param name="pPath">The Path to load Data from, such as 'Player Data/Albert'.</param>
        /// <returns></returns>
        public static T LoadFile<T>(string pPath, string encryptKey = "")
        {
            string dataPath = Application.persistentDataPath + "/" + pPath + ".data";

            if (File.Exists(dataPath))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                FileStream stream = new FileStream(dataPath, FileMode.Open);
                byte[] bytes = (byte[])formatter.Deserialize(stream);
                string decrypted;
                aes.Key = (encryptKey == "") ? key : GetBytes(encryptKey);
                aes.GenerateIV();

                try
                {
                    decrypted = DecryptStringFromBytes_Aes(bytes, aes.Key, aes.IV);
                    ES_Data<T> Data = Deserialize<ES_Data<T>>(FixDecrypted(decrypted));
                    stream.Close();
                    return Data.SaveData;
                }
                catch
                {
                    stream?.Close();
                    return default;
                }
            }

            Debug.LogError("The given save file '" + pPath + "' does not exist.");
            return default;
        }
        /// <summary> Returns a Data object from encrypted memory, if it exists. </summary>
        /// <param name="pKey">The key to load Data from, such as 'Albert'.</param>
        /// <returns></returns>
        public static T LoadPref<T>(string pKey)
        {
            if (PlayerPrefs.HasKey(pKey))
            {
                byte[] bytes = GetBytes(PlayerPrefs.GetString(pKey));
                string decrypted;
                aes.Key = key;
                aes.GenerateIV();
                decrypted = DecryptStringFromBytes_Aes(bytes, aes.Key, aes.IV);
                ES_Data<T> Data = Deserialize<ES_Data<T>>(FixDecrypted(decrypted));

                return Data.SaveData;
            }

            Debug.LogError("The given save file '" + pKey + "' does not exist.");
            return default;
        }
    }
}