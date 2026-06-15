using System.IO;
using UnityEngine;

namespace Game.Utilities
{
    /// <summary> This gives the methods needed to save data. </summary>
    public static class MS_EncryptedData
    {
        /// <summary> Checks if a certain file has been saved already. </summary>
        /// <param name="path">The Path of the file, such as 'gamesettings'.</param>
        public static bool ExistsFile(string path)
        {
            string dataPath = Application.persistentDataPath + "/" + path + ".data";
            return File.Exists(dataPath);
        }
        /// <summary> Checks if a certain key has been saved already. </summary>
        /// <param name="key">The key of the save, such as 'gamesettings'.</param>
        public static bool ExistsPref(string key)
        { return PlayerPrefs.HasKey(key); }

        /// <summary> Deletes a save file if it exists. </summary>
        /// <param name="path">The Path of the file, such as 'gamesettings'.</param>
        /// <returns></returns>
        public static void DeleteDataFile(string path)
        {
            string dataPath = Application.persistentDataPath + "/" + path + ".data";

            if (File.Exists(dataPath)) File.Delete(dataPath);
            else Debug.LogError("The given save file '" + path + "' does not exist.");
        }
        /// <summary> Deletes a save key if it exists. </summary>
        /// <param name="key">The key of the save, such as 'gamesettings'.</param>
        /// <returns></returns>
        public static void DeleteDataPref(string key)
        {
            if (PlayerPrefs.HasKey(key)) PlayerPrefs.DeleteKey(key);
            else Debug.LogError("The given save key '" + key + "' does not exist.");
        }

        /// <summary> Saves Data into encrypted memory. </summary>
        /// <param name="data">The Data to save, such as 'gamesettings', or 0, 1.5, false, and so on.</param>
        /// <param name="path">The Path to save Data into, such as 'Player Data/gamesettings'.</param>
        public static void SaveFile<T>(T data, string path, string encryptKey = "")
        { 
            MS_Encryption.SaveFile(data, path, encryptKey); 
        }

        /// <summary> Saves Data into encrypted memory. </summary>
        /// <param name="data">The Data to save, such as 'gamesettings', or 0, 1.5, false, and so on.</param>
        /// <param name="key">The key to save Data into, such as 'gamesettings'.</param>
        public static void SavePref<T>(T data, string key)
        {
            MS_Encryption.SavePref(data, key);
        }

        /// <summary> Returns a Data object from encrypted memory, if it exists. </summary>
        /// <param name="path">The Path to load Data from, such as 'Player Data/gamesettings'.</param>
        /// <returns></returns>
        public static T LoadFile<T>(string path, T defaultValue, string encryptKey = "")
        {
            if (ExistsFile(path))
            {
                return MS_Encryption.LoadFile<T>(path, encryptKey);
            }
            return defaultValue;
        }

        /// <summary> Returns a Data object from encrypted memory, if it exists. </summary>
        /// <param name="key">The key to load Data from, such as 'gamesettings'.</param>
        /// <returns></returns>
        public static T LoadPref<T>(string key, T defaultValue)
        {
            if (ExistsPref(key))
            {
                return MS_Encryption.LoadPref<T>(key);
            }
            return defaultValue;
        }
    }
}